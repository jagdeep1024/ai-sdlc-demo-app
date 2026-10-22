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


def render_locked(folder, events):
    lines = ['# AI interaction log', '',
             'Generated from append-only `ai-interactions.jsonl`. Sources and speakers identify actual provenance.',
             'Retrospective summaries are labelled; hook configuration alone does not prove automatic capture.', '']
    for event in events:
        lines += [f"## {event['id']}", '', f"- Time: {event['timestamp']}",
                  f"- Source: {event['source']}", f"- Speaker: {event['speaker']}",
                  f"- Event: {event['event']}", '', '```json',
                  json.dumps(event['data'], ensure_ascii=False, indent=2, sort_keys=True).replace('`', '\\u0060'), '```', '']
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
    with locked(root) as folder:
        events = read_events(folder)
        previous = next((item for item in events if item['id'] == event['id']), None)
        if previous:
            if any(previous[key] != event[key] for key in ('source', 'speaker', 'event', 'data')):
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
    parser.add_argument('command', choices=('append', 'render', 'hook'))
    parser.add_argument('--root', type=Path, default=ROOT, help='alternate evidence root (tests only)')
    args = parser.parse_args()
    if args.command == 'render':
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
