// Catalogue of BCRA credit status codes (field 6) → human-readable description.
// Kept on the frontend only (the API returns the raw 2-digit code).
// Codes outside this catalogue are shown as the raw code alone.
const CREDIT_STATUS_DESCRIPTIONS = {
  "01": "Situación normal",
  21: "Riesgo bajo",
  23: "En tratamiento especial",
  "03": "Riesgo medio",
  "04": "Riesgo alto",
  "05": "Irrecuperable",
  11: 'Con asistencias cubiertas con garantías preferidas "A"',
};

// Display order for the selector, matching the "Codificación del campo 6" table in the BCRA spec
// (LEAME DEUDORES). An explicit array is required: numeric-like object keys ("01", "21", ...) are
// iterated in ascending numeric order by the JS engine, which would ignore the spec order.
const CREDIT_STATUS_ORDER = ["01", "21", "23", "03", "04", "05", "11"];

/** Options for a credit-status selector: [{ code, label }], in the BCRA spec order. */
export const CREDIT_STATUS_OPTIONS = CREDIT_STATUS_ORDER.map((code) => ({
  code,
  label: `${code} – ${CREDIT_STATUS_DESCRIPTIONS[code]}`,
}));

/**
 * Returns a display label for a credit status code, e.g. "05 – Irrecuperable".
 * If the code is unknown, returns just the code.
 * @param {string} code
 * @returns {string}
 */
export function describeCreditStatus(code) {
  if (!code) return "";
  const description = CREDIT_STATUS_DESCRIPTIONS[code];
  return description ? `${code} – ${description}` : code;
}
