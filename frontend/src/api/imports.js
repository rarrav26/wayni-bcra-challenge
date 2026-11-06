import apiClient from './client';

/**
 * Uploads the BCRA file to start asynchronous processing.
 * The API responds 202 Accepted with the created import job id; the actual
 * processing happens in the background (Worker), so there is no progress to poll here.
 *
 * @param {File} file - The deudores.txt file to upload.
 * @returns {Promise<{ importJobId: string }>}
 */
export async function uploadFile(file) {
  const formData = new FormData();
  formData.append('file', file);

  const { data } = await apiClient.post('/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}
