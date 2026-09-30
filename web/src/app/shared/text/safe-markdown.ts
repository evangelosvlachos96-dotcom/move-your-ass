/**
 * Renders the restricted Markdown the About page allows: paragraphs, **bold**, and `- ` lists.
 *
 * **Escapes first, formats second.** Every character of input is HTML-escaped before a single tag
 * is produced, so there is no ordering in which user text can become markup. The server also
 * strips tags on the way in, and Angular sanitises `[innerHTML]` on the way out; this is the
 * middle of three, and the only one that would still hold if the other two were removed.
 *
 * A markdown library was not used on purpose. Every one of them supports raw HTML by default,
 * and this needs three constructs.
 */
export function renderSafeMarkdown(source: string | null | undefined): string {
  if (!source) {
    return '';
  }

  const blocks = escapeHtml(source)
    .replace(/\r\n?/g, '\n')
    .split(/\n{2,}/)
    .map((block) => block.trim())
    .filter((block) => block.length > 0);

  return blocks.map(renderBlock).join('');
}

function renderBlock(block: string): string {
  const lines = block.split('\n').map((line) => line.trim());

  if (lines.every((line) => /^[-*]\s+/.test(line))) {
    const items = lines.map((line) => `<li>${inline(line.replace(/^[-*]\s+/, ''))}</li>`).join('');
    return `<ul>${items}</ul>`;
  }

  // A single newline inside a paragraph is a line break, which is what people expect from a
  // textarea even though Markdown proper would swallow it.
  return `<p>${lines.map(inline).join('<br>')}</p>`;
}

/** Bold only. Everything else has already been escaped and stays literal. */
function inline(text: string): string {
  return text.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
