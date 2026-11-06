import apiClient from "./client";

/**
 * Gets a debtor by identification number.
 * @param {string} identificationNumber
 * @returns {Promise<{ nro_identificacion: string, situacion_maxima: string, suma_total_prestamos: number }>}
 */
export async function getDebtor(identificationNumber) {
  const { data } = await apiClient.get(`/deudores/${identificationNumber}`);
  return data;
}

/**
 * Gets the top N debtors by total financing amount (highest first).
 * Optionally filters by max credit status (situación).
 * @param {number} n
 * @param {string} [situacion] Optional 2-digit credit status code to filter by.
 * @returns {Promise<Array<{ nro_identificacion: string, situacion_maxima: string, suma_total_prestamos: number }>>}
 */
export async function getTopDebtors(n, situacion) {
  const params = situacion ? { situacion } : undefined;
  const { data } = await apiClient.get(`/deudores/top/${n}`, { params });
  return data;
}
