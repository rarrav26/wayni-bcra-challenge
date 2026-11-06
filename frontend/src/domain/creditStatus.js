// Catalogue of BCRA credit status codes (field 6) → human-readable description.
// Kept on the frontend only (the API returns the raw 2-digit code, per ADR-013/ADR-014).
// Codes outside this catalogue are shown as the raw code alone.
const CREDIT_STATUS_DESCRIPTIONS = {
  '01': 'Normal',
  '03': 'Riesgo medio',
  '04': 'Riesgo alto',
  '05': 'Irrecuperable',
  '11': 'Con garantías preferidas "A"',
  '21': 'Riesgo bajo',
  '23': 'Tratamiento especial',
};

/**
 * Returns a display label for a credit status code, e.g. "05 – Irrecuperable".
 * If the code is unknown, returns just the code.
 * @param {string} code
 * @returns {string}
 */
export function describeCreditStatus(code) {
  if (!code) return '';
  const description = CREDIT_STATUS_DESCRIPTIONS[code];
  return description ? `${code} – ${description}` : code;
}
