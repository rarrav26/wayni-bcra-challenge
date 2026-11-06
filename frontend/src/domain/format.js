// Label for the financing-amount field. Per the BCRA spec, amounts are expressed in
// thousands of pesos with one decimal; the unit is shown so the figure is unambiguous.
export const AMOUNT_LABEL = "Suma total de préstamos (en miles de $)";

// Formats a financing amount (thousands of pesos, one decimal) for display using
// Argentine locale conventions (thousands separator and decimal comma).
const amountFormatter = new Intl.NumberFormat("es-AR", {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

/**
 * @param {number} amount
 * @returns {string}
 */
export function formatAmount(amount) {
  if (amount == null || Number.isNaN(amount)) return "-";
  return amountFormatter.format(amount);
}
