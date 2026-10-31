#!/usr/bin/env python3
"""Append redacted assignment events and deterministically render their public log."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
SECRET_KEY = re.compile(r'(password|passwd|secret|token|api[_-]?key|authorization|cookie|credential)', re.I)
PATTERNS = [
    (re.compile(r'\b(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]+', re.I), '[REDACTED AUTH]'),
    (re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|sk-[A-Za-z0-9_-]{12,})\b'), '[REDACTED TOKEN]'),
    (re.compile(r'(?i)(authorization\s*:\s*)[^\r\n]+'), r'\1[REDACTED]'),
    (re.compile(r'''(?i)((?:password|passwd|secret|token|api[_-]?key|cookie)\s*[=:]\s*)["']?[^\s,"';}]+'''), r'\1[REDACTED]'),
]


def redact(value):
    if isinstance(value, dict):
        return {str(k): '[REDACTED]' if SECRET_KEY.search(str(k)) else redact(v) for k, v in value.items()}
    if isinstance(value, list):
        return [redact(item) for item in value]
    if isinstance(value, str):
        # Project demo credentials are public fixtures but unnecessary in evidence.
        for secret in ['ChangeMe123!'] + json.loads(os.environ.get('AI_LOG_REDACT_VALUES', '[]')):
            if not isinstance(secret, str) or not secret:
                raise ValueError('redaction values must be nonempty strings')
            value = value.replace(secret, '[REDACTED]')
        for pattern, replacement in PATTERNS:
            value = pattern.sub(replacement, value)
        return value
    return value


@contextmanager
def locked(root):
    # Lock survives replacement of the readable output and is never public evidence.
    folder = root / 'documentation'
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / '.ai-log.lock').open('a') as handle:
        fcntl.flock(handle, fcntl.LOCK_EX)
        yield folder


def read_events(folder):
    source = folder / 'ai-interactions.jsonl'
    if not source.exists():
        return []
    return [json.loads(line) for line in source.read_text().splitlines() if line.strip()]


REVIEW_FIELDS = (
    'stage', 'agent', 'agent_name', 'agent_type', 'ai_tool', 'model', 'mode',
    'related_ids', 'parent_task', 'context', 'request', 'skills_used',
    'subagents_used', 'mcp_tools_used', 'tools_used', 'suggestion',
    'human_decision', 'action_verification', 'limitation', 'status', 'artifacts',
)
LIST_FIELDS = ('related_ids', 'skills_used', 'subagents_used', 'mcp_tools_used', 'tools_used', 'artifacts')


def normalize(event, metadata=None):
    """Add reviewer fields without changing the original evidence envelope/payload."""
    if event.get('schema_version') == 2 and not metadata:
        return event
    metadata = metadata or {}
    data = event['data'] if isinstance(event['data'], dict) else {}
    human = event['source'] in ('human-message', 'human-message-summary')
    delegated = event['source'] in ('agent-delegation', 'agent-review', 'implementation-agent', 'mutation-review-agent')
    request = data.get('prompt', data.get('request', data.get('Objective', data.get('summary'))))
    defaults = dict(
        stage=None, agent='human' if human else 'agent',
        agent_name=None if human else event['speaker'],
        agent_type='sub-agent' if delegated and event['speaker'] != 'main-agent' else None,
        ai_tool='Codex', model=data.get('model'), mode=None,
        related_ids=[], parent_task=None, context='Assignment for AI in SDLC',
        request=request, skills_used=[], subagents_used=[], mcp_tools_used=[],
        tools_used=[], suggestion=data.get('suggestion'),
        human_decision=event['data'] if human else None,
        action_verification=data.get('Verification', data.get('verification', data.get('results'))),
        limitation='Unrecorded fields are null or empty arrays; these do not mean the capability was unused. Original source, speaker, event and data are retained. AI tool is identified from the assignment workflow; absent model/mode/tool details are not reconstructed.',
        status='recorded', artifacts=[],
    )
    result = dict(event)
    for key in REVIEW_FIELDS:
        value = metadata.get(key, defaults[key])
        if key in LIST_FIELDS and not isinstance(value, list):
            raise ValueError(f'{key} must be an array')
        result[key] = value
    result['schema_version'] = 2
    result['normalization'] = {
        'kind': 'schema-enrichment',
        'note': 'Reviewer fields added from retained evidence and explicitly supplied metadata; not a new execution or a claim of complete historical capture.',
    }
    return result


def migrate(root):
    """Explicit one-time schema migration; retain every original event and timestamp."""
    with locked(root) as folder:
        events = read_events(folder)
        converted = []
        stage = 'setup'
        # Stages follow the retained authorization/checkpoint sequence, not invented run times.
        for event in events:
            identifier = event['id']
            if identifier == '80d2b0f4-d560-4350-9cfa-841ec6253ea0':
                stage = 'single-approver'
            elif identifier == 'stage-two-human-authorization':
                stage = 'dual-approval'
            elif identifier == 'stage-three-human-authorization':
                stage = 'browser-mutation-ci'
            elif identifier == 'pr-review-human-authorization':
                stage = 'pr-review'
            elif identifier == 'video-completion-human-report-20261031':
                stage = 'video-delivery'
            converted.append(event if event.get('schema_version') == 2 else normalize(event, {'stage': stage}))
        source = folder / 'ai-interactions.jsonl'
        if converted != events:
            with tempfile.NamedTemporaryFile('w', dir=folder, delete=False, encoding='utf-8') as handle:
                for event in converted:
                    handle.write(json.dumps(event, ensure_ascii=False, sort_keys=True) + '\n')
                handle.flush()
                os.fsync(handle.fileno())
                temp = handle.name
            os.replace(temp, source)
        render_locked(folder, converted)


def render_locked(folder, events):
    lines = ['# AI augmentation log', '',
             'Generated from `ai-interactions.jsonl` in the reviewer’s suggested format.',
             'Original source, speaker, event, timestamps and payloads remain attached to each record.',
             'Unknown fields are null/empty; recorded does not mean applied or successful. Historical summaries and agent decisions retain their attribution.', '']
    for original in events:
        event = normalize(original)
        lines += [f"## {event['id']}", '', f"- Time: {event['timestamp']}",
                  f"- Source: {event['source']}", f"- Speaker: {event['speaker']}",
                  f"- Event: {event['event']}", f"- Status: {event['status']}", '', '```json',
                  json.dumps(event, ensure_ascii=False, indent=2, sort_keys=True).replace('`', '\\u0060'), '```', '']
    with tempfile.NamedTemporaryFile('w', dir=folder, delete=False, encoding='utf-8') as handle:
        handle.write('\n'.join(lines))
        temp = handle.name
    os.replace(temp, folder / 'AI-INTERACTION-LOG.md')


def append(root, supplied):
    for key in ('source', 'speaker', 'event', 'data'):
        if key not in supplied:
            raise ValueError(f'missing {key}')
    for key in ('source', 'speaker', 'event'):
        if not isinstance(supplied[key], str) or not re.fullmatch(r'[A-Za-z0-9_./:-]+', supplied[key]):
            raise ValueError(f'{key} must be a nonempty identifier')
    event = redact({key: supplied[key] for key in ('source', 'speaker', 'event', 'data')})
    event['id'] = str(supplied.get('id') or uuid.uuid4())
    if not re.fullmatch(r'[A-Za-z0-9_.:-]+', event['id']):
        raise ValueError('invalid event id')
    event['timestamp'] = datetime.now(timezone.utc).isoformat()
    event = normalize(event, redact({key: supplied[key] for key in REVIEW_FIELDS if key in supplied}))
    with locked(root) as folder:
        events = read_events(folder)
        previous = next((item for item in events if item['id'] == event['id']), None)
        if previous:
            if any(previous[key] != event[key] for key in ('source', 'speaker', 'event', 'data')) or any(previous.get(key) != event[key] for key in REVIEW_FIELDS if key in supplied):
                raise ValueError('event ID already exists with different content')
        else:
            with (folder / 'ai-interactions.jsonl').open('a', encoding='utf-8') as handle:
                handle.write(json.dumps(event, ensure_ascii=False, sort_keys=True) + '\n')
                handle.flush()
                os.fsync(handle.fileno())
            events.append(event)
        render_locked(folder, events)
    return event['id']


def hook_event(payload):
    name = payload.get('hook_event_name')
    if name not in ('UserPromptSubmit', 'Stop', 'SubagentStart', 'SubagentStop'):
        raise ValueError('unsupported hook event')
    data = {key: payload[key] for key in ('session_id', 'turn_id', 'agent_id', 'agent_type', 'model') if key in payload}
    if name == 'UserPromptSubmit':
        data['prompt'] = payload.get('prompt', '')
    if name in ('Stop', 'SubagentStop'):
        data['response'] = payload.get('last_assistant_message')
    # Never read transcript paths: only event fields belong to this capture.
    digest = hashlib.sha256(json.dumps({'event': name, 'data': data}, sort_keys=True).encode()).hexdigest()
    return {'id': 'hook-' + digest, 'source': 'codex-hook',
            'speaker': 'prompt-submitter' if name == 'UserPromptSubmit' else 'agent', 'event': name, 'data': data}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('append', 'render', 'hook', 'migrate'))
    parser.add_argument('--root', type=Path, default=ROOT, help='alternate evidence root (tests only)')
    args = parser.parse_args()
    if args.command == 'migrate':
        migrate(args.root)
    elif args.command == 'render':
        with locked(args.root) as folder:
            render_locked(folder, read_events(folder))
    else:
        payload = json.load(sys.stdin)
        if args.command == 'hook':
            # Reject use from another project, even if this command was copied there.
            cwd = Path(payload.get('cwd', '')).resolve()
            if not cwd.is_relative_to(args.root.resolve()):
                raise ValueError('hook cwd is outside this assignment')
            append(args.root, hook_event(payload))
            print('{}')
        else:
            print(append(args.root, payload))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as error:
        print(f'AI evidence logging failed: {error}', file=sys.stderr)
        sys.exit(1)
