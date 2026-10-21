import concurrent.futures
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / 'ai_log.py'
spec = importlib.util.spec_from_file_location('ai_log', SCRIPT)
logger = importlib.util.module_from_spec(spec)
spec.loader.exec_module(logger)


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def event(self, identifier='actual-1'):
        return dict(id=identifier, source='agent-delegation', speaker='main-agent',
                    event='prompt', data={'prompt': 'Inspect domain rules.'})

    def test_secrets_are_removed_before_source_is_written(self):
        event = self.event()
        event['data'] = {'password': 'hidden', 'nested': [{'access_token': 'hidden2'}],
                         'prompt': 'Authorization: Bearer abc.def\npassword=badvalue token=alsoBad ChangeMe123! ghp_abcdefghijklmnop custom-secret',
                         'headers': {'Cookie': 'session=secret'}}
        with patch.dict(os.environ, {'AI_LOG_REDACT_VALUES': '["custom-secret"]'}):
            logger.append(self.root, event)
        for path in (self.root / 'documentation').glob('*.*'):
            public = path.read_text()
            for secret in ['hidden', 'abc.def', 'badvalue', 'alsoBad', 'ChangeMe123!', 'ghp_abcdefghijklmnop', 'custom-secret', 'session=secret']:
                self.assertNotIn(secret, public)
        self.assertIn('[REDACTED]', public)

    def test_retry_does_not_duplicate_or_change_bytes(self):
        logger.append(self.root, self.event())
        source = self.root / 'documentation/ai-interactions.jsonl'
        readable = self.root / 'documentation/AI-INTERACTION-LOG.md'
        before = (source.read_bytes(), readable.read_bytes())
        logger.append(self.root, self.event())
        self.assertEqual(before, (source.read_bytes(), readable.read_bytes()))
        changed = self.event()
        changed['data']['prompt'] = 'Different prompt'
        with self.assertRaises(ValueError):
            logger.append(self.root, changed)
        self.assertEqual(before, (source.read_bytes(), readable.read_bytes()))

    def test_parallel_processes_keep_every_event_and_render_consistently(self):
        def write(index):
            return subprocess.run([sys.executable, str(SCRIPT), 'append', '--root', str(self.root)],
                                  input=json.dumps(self.event(f'parallel-{index}')), text=True,
                                  capture_output=True, check=True)
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
            list(executor.map(write, range(24)))
        source = self.root / 'documentation/ai-interactions.jsonl'
        events = [json.loads(line) for line in source.read_text().splitlines()]
        self.assertEqual(24, len(events))
        self.assertEqual(24, len({event['id'] for event in events}))
        readable = self.root / 'documentation/AI-INTERACTION-LOG.md'
        before = readable.read_bytes()
        with logger.locked(self.root) as folder:
            logger.render_locked(folder, logger.read_events(folder))
        self.assertEqual(before, readable.read_bytes())

    def test_hook_uses_only_current_event_fields_and_returns_json(self):
        payload = dict(cwd=str(self.root), hook_event_name='Stop', session_id='assignment',
                       turn_id='one', last_assistant_message='Completed stage one.',
                       transcript_path='/must/not/be/read', unknown='private')
        result = subprocess.run([sys.executable, str(SCRIPT), 'hook', '--root', str(self.root)],
                                input=json.dumps(payload), text=True, capture_output=True, check=True)
        self.assertEqual({}, json.loads(result.stdout))
        source = (self.root / 'documentation/ai-interactions.jsonl').read_text()
        self.assertIn('Completed stage one.', source)
        self.assertNotIn('/must/not/be/read', source)
        self.assertNotIn('private', source)
        payload['cwd'] = str(self.root.parent)
        result = subprocess.run([sys.executable, str(SCRIPT), 'hook', '--root', str(self.root)],
                                input=json.dumps(payload), text=True, capture_output=True)
        self.assertNotEqual(0, result.returncode)

    def test_all_documented_hook_events_have_correct_payload(self):
        for name in ['UserPromptSubmit', 'SubagentStart', 'SubagentStop', 'Stop']:
            with self.subTest(name=name):
                event = logger.hook_event(dict(hook_event_name=name, prompt='Actual prompt',
                                              last_assistant_message='Actual response', agent_id='worker'))
                self.assertEqual('codex-hook', event['source'])
                self.assertEqual(name, event['event'])
                self.assertEqual(name == 'UserPromptSubmit', 'prompt' in event['data'])
                self.assertEqual(name in ['Stop', 'SubagentStop'], 'response' in event['data'])


if __name__ == '__main__':
    unittest.main()
