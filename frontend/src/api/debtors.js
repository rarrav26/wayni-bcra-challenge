import apiClient from './client';

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
 * @param {number} n
 * @returns {Promise<Array<{ nro_identificacion: string, situacion_maxima: string, suma_total_prestamos: number }>>}
 */
export async function getTopDebtors(n) {
  const { data } = await apiClient.get(`/deudores/top/${n}`);
  return data;
}
