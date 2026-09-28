import { describe, expect, it } from 'vitest';
import { renderSafeMarkdown } from './safe-markdown';

describe('renderSafeMarkdown', () => {
  it('produces nothing for nothing', () => {
    expect(renderSafeMarkdown(null)).toBe('');
    expect(renderSafeMarkdown(undefined)).toBe('');
    expect(renderSafeMarkdown('   ')).toBe('');
  });

  it('wraps a block of text in a paragraph', () => {
    expect(renderSafeMarkdown('Γεια σου')).toBe('<p>Γεια σου</p>');
  });

  it('starts a new paragraph on a blank line and breaks on a single newline', () => {
    expect(renderSafeMarkdown('Πρώτη\n\nΔεύτερη')).toBe('<p>Πρώτη</p><p>Δεύτερη</p>');
    expect(renderSafeMarkdown('Πρώτη\nδεύτερη γραμμή')).toBe('<p>Πρώτη<br>δεύτερη γραμμή</p>');
  });

  it('renders a list when every line of a block is a bullet', () => {
    expect(renderSafeMarkdown('- ένα\n- δύο')).toBe('<ul><li>ένα</li><li>δύο</li></ul>');
    expect(renderSafeMarkdown('* ένα\n* δύο')).toBe('<ul><li>ένα</li><li>δύο</li></ul>');
    // A single bulleted line among prose is prose, not a broken list.
    expect(renderSafeMarkdown('κείμενο\n- ένα')).toBe('<p>κείμενο<br>- ένα</p>');
  });

  it('renders bold', () => {
    expect(renderSafeMarkdown('πολύ **σημαντικό** αυτό')).toBe(
      '<p>πολύ <strong>σημαντικό</strong> αυτό</p>',
    );
  });

  it('escapes markup before it formats anything', () => {
    expect(renderSafeMarkdown('<script>alert(1)</script>')).toBe(
      '<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>',
    );
    expect(renderSafeMarkdown('<img src=x onerror="steal()">')).not.toContain('<img');
    expect(renderSafeMarkdown('a & b')).toBe('<p>a &amp; b</p>');
    expect(renderSafeMarkdown(`he said "hi" and 'bye'`)).toBe(
      '<p>he said &quot;hi&quot; and &#39;bye&#39;</p>',
    );
  });

  it('cannot be tricked into producing a tag by mixing markup with formatting', () => {
    // Bold applied after escaping means the angle brackets are already inert.
    const rendered = renderSafeMarkdown('**<b>bold</b>**');
    expect(rendered).toBe('<p><strong>&lt;b&gt;bold&lt;/b&gt;</strong></p>');
    expect(rendered).not.toContain('<b>');
  });

  it('leaves an unmatched asterisk alone rather than producing a stray tag', () => {
    expect(renderSafeMarkdown('2 ** 3 = 8')).toBe('<p>2 ** 3 = 8</p>');
    expect(renderSafeMarkdown('**ανοιχτό')).toBe('<p>**ανοιχτό</p>');
  });

  it('normalises Windows line endings', () => {
    expect(renderSafeMarkdown('α\r\n\r\nβ')).toBe('<p>α</p><p>β</p>');
  });
});
