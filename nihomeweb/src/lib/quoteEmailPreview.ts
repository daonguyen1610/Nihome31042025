export function quoteEmailPreviewHtml(body: string): string {
  if (body.trimStart().startsWith("<")) return body;
  const escaped = body.replace(/[&<>"]/g, (character) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;",
  })[character] ?? character);
  return `<div style="white-space:pre-wrap;font-family:Arial,sans-serif">${escaped}</div>`;
}
