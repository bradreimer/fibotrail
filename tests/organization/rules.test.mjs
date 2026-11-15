// Unit tests for the rules themselves, against good and bad examples.

import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import {
  ADR_SECTIONS,
  SPEC_SECTIONS,
  checkAdr,
  checkAdrNumbering,
  checkEntries,
  checkImageName,
  checkIndex,
  checkIssueTemplate,
  checkKnowledgeName,
  checkMentions,
  checkSpec,
  checkStatus,
  headings,
  isRelativeLink,
  linkTargets,
  stripCodeBlocks,
} from './rules.mjs';

const adr = ({ number = '001', status = 'Accepted', sections = ADR_SECTIONS } = {}) =>
  [`# ADR-${number}: A decision`, '']
    .concat(sections.flatMap((s) => [`## ${s}`, '', s === 'Status' ? status : 'Text.', '']))
    .join('\n');

const spec = ({ tracking = 'Tracking issue: [#7](https://github.com/bradreimer/fibotrail/issues/7).', sections = SPEC_SECTIONS } = {}) =>
  ['# A capability', '', tracking, '']
    .concat(sections.flatMap((s) => [`## ${s}`, '', 'Text.', '']))
    .join('\n');

describe('Markdown helpers', () => {
  it('ignores headings and links inside code blocks', () => {
    const markdown = '## Real\n\n```markdown\n## Example\n[x](missing.md)\n```\n\n[y](real.md)\n';
    assert.deepEqual(headings(markdown, 2), ['Real']);
    assert.deepEqual(linkTargets(markdown), ['real.md']);
    assert.ok(!stripCodeBlocks(markdown).includes('Example'));
  });

  it('finds links and images, and ignores code spans', () => {
    const markdown = 'See [a](a.md#part), ![b](images/b.svg), and `[c](c.md)`.';
    assert.deepEqual(linkTargets(markdown), ['a.md#part', 'images/b.svg']);
  });

  it('tells relative links from web links and anchors', () => {
    assert.ok(isRelativeLink('../adr/README.md'));
    assert.ok(!isRelativeLink('https://example.com'));
    assert.ok(!isRelativeLink('mailto:someone@example.com'));
    assert.ok(!isRelativeLink('#section'));
  });
});

describe('checkEntries', () => {
  it('passes when every entry is required or optional', () => {
    assert.deepEqual(checkEntries(['a', 'b'], ['a'], ['b'], 'root'), []);
  });

  it('reports a missing required entry and a stray entry', () => {
    const problems = checkEntries(['stray.txt'], ['a'], [], 'root');
    assert.equal(problems.length, 2);
    assert.match(problems[0], /"a" is missing/);
    assert.match(problems[1], /"stray.txt" isn't allowed/);
  });
});

describe('checkAdr', () => {
  it('passes a well-formed ADR', () => {
    assert.deepEqual(checkAdr('ADR-001-a-decision.md', adr()), []);
  });

  it('rejects a bad file name', () => {
    assert.equal(checkAdr('adr-1-Decision.md', adr()).length, 1);
    assert.equal(checkAdr('ADR-01-decision.md', adr()).length, 1);
  });

  it('rejects a title number that differs from the file number', () => {
    assert.match(checkAdr('ADR-002-a-decision.md', adr()).join(), /doesn't match/);
  });

  it('rejects missing, extra, or reordered sections', () => {
    const missing = adr({ sections: ADR_SECTIONS.filter((s) => s !== 'Consequences') });
    const extra = adr({ sections: [...ADR_SECTIONS, 'Notes'] });
    const reordered = adr({ sections: [...ADR_SECTIONS].reverse() });
    for (const markdown of [missing, extra, reordered]) {
      assert.match(checkAdr('ADR-001-a-decision.md', markdown).join(), /sections must be exactly/);
    }
  });

  it('rejects an unknown status', () => {
    assert.match(checkAdr('ADR-001-a-decision.md', adr({ status: 'Done' })).join(), /Status must be/);
  });

  it('accepts a superseded status only when the other ADR exists', () => {
    const markdown = adr({ status: 'Superseded by ADR-002' });
    const names = ['ADR-001-a-decision.md', 'ADR-002-a-better-decision.md'];
    assert.deepEqual(checkAdr(names[0], markdown, names), []);
    assert.match(checkAdr(names[0], markdown, [names[0]]).join(), /doesn't exist/);
  });
});

describe('checkAdrNumbering', () => {
  it('passes numbers that run in order', () => {
    assert.deepEqual(checkAdrNumbering(['ADR-001-a.md', 'ADR-002-b.md', 'README.md']), []);
  });

  it('rejects a gap, a duplicate, and a start above 001', () => {
    assert.equal(checkAdrNumbering(['ADR-001-a.md', 'ADR-003-c.md']).length, 1);
    assert.equal(checkAdrNumbering(['ADR-001-a.md', 'ADR-001-b.md']).length, 1);
    assert.equal(checkAdrNumbering(['ADR-002-b.md']).length, 1);
  });
});

describe('checkSpec', () => {
  it('passes a well-formed spec', () => {
    assert.deepEqual(checkSpec('trip-inbox.md', spec()), []);
  });

  it('rejects a bad file name', () => {
    assert.equal(checkSpec('Trip_Inbox.md', spec()).length, 1);
  });

  it('rejects a spec with no tracking issue', () => {
    assert.match(checkSpec('trip-inbox.md', spec({ tracking: '' })).join(), /Tracking issue/);
  });

  it('rejects a tracking issue in another repository', () => {
    const tracking = 'Tracking issue: [#7](https://github.com/someone/else/issues/7).';
    assert.match(checkSpec('trip-inbox.md', spec({ tracking })).join(), /must link to/);
  });

  it('rejects missing sections', () => {
    const markdown = spec({ sections: SPEC_SECTIONS.filter((s) => s !== 'Non-goals') });
    assert.match(checkSpec('trip-inbox.md', markdown).join(), /sections must be exactly/);
  });
});

describe('knowledge rules', () => {
  it('checks document and image names', () => {
    assert.deepEqual(checkKnowledgeName('immich-api.md'), []);
    assert.equal(checkKnowledgeName('Immich API.md').length, 1);
    assert.deepEqual(checkImageName('logo-concept.svg'), []);
    assert.equal(checkImageName('Logo.SVG').length, 1);
    assert.equal(checkImageName('diagram.pdf').length, 1);
  });
});

describe('checkIndex', () => {
  it('reports each document the index does not link to', () => {
    const index = '| [one](one.md) | [two](./two.md) |\n\n```markdown\n[three](three.md)\n```\n';
    assert.deepEqual(checkIndex(index, ['one.md', 'two.md']), []);
    assert.deepEqual(checkIndex(index, ['one.md', 'three.md']), ["the index doesn't link to three.md"]);
  });
});

describe('checkIssueTemplate', () => {
  const template = (label, sections) =>
    `---\nname: A\nabout: B\ntitle: "[C]: "\nlabels: ["${label}"]\n---\n\n` +
    sections.map((s) => `## ${s}\n`).join('\n');

  it('passes a complete template', () => {
    const sections = ['Problem', 'Proposed solution', 'Related spec'];
    assert.deepEqual(checkIssueTemplate('feature_request.md', template('enhancement', sections)), []);
  });

  it('rejects a wrong label, a missing section, and an unknown template', () => {
    assert.match(
      checkIssueTemplate('feature_request.md', template('bug', ['Problem'])).join(),
      /labels must include "enhancement".*"Proposed solution" is missing/,
    );
    assert.equal(checkIssueTemplate('task.md', template('task', [])).length, 1);
  });
});

describe('checkMentions and checkStatus', () => {
  it('reports each missing phrase', () => {
    assert.deepEqual(checkMentions('See docs/adr/.', ['docs/adr/', 'docs/specs/']), [
      'must mention "docs/specs/"',
    ]);
  });

  it('reports each missing status section', () => {
    const markdown = '# Status\n\n## Current milestone\n\n## Completed\n\n## Next\n';
    assert.deepEqual(checkStatus(markdown), ['section "In progress" is missing']);
  });
});
