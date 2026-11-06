import apiClient from './client';

/**
 * Gets a financial entity by its code.
 * @param {string} code
 * @returns {Promise<{ codigo_entidad: string, suma_total_prestamos: number }>}
 */
export async function getEntity(code) {
  const { data } = await apiClient.get(`/entidades/${code}`);
  return data;
}
