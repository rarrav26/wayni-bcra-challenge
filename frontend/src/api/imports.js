import axios from "axios";
import apiClient from "./client";

/**
 * Uploads the BCRA file using a pre-signed URL (direct-to-S3), so large files do not pass through
 * the API. Two steps:
 *   1. POST /upload with the file name -> the API registers the import and returns a pre-signed URL.
 *   2. PUT the file directly to that URL (S3). Processing is then triggered by the S3 event.
 *
 * @param {File} file - The deudores.txt file to upload.
 * @param {(percent: number) => void} [onProgress] - Optional upload progress callback (0-100).
 * @returns {Promise<{ importJobId: string }>}
 */
export async function uploadFile(file, onProgress) {
  // Step 1: register the import and get the pre-signed upload URL.
  const { data } = await apiClient.post("/upload", { fileName: file.name });
  const { importJobId, uploadUrl } = data;

  // Step 2: upload the file directly to S3 with the pre-signed URL (plain PUT, no API involved).
  // A bare axios instance is used so the API client's base URL/headers are not applied.
  await axios.put(uploadUrl, file, {
    headers: { "Content-Type": "application/octet-stream" },
    onUploadProgress: (event) => {
      if (onProgress && event.total) {
        onProgress(Math.round((event.loaded / event.total) * 100));
      }
    },
  });

  return { importJobId };
}
