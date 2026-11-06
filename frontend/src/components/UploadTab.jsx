import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { uploadFile } from '../api/imports';

export default function UploadTab() {
  const [file, setFile] = useState(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  const handleFileChange = (event) => {
    setFile(event.target.files?.[0] ?? null);
    setResult(null);
    setError(null);
  };

  const handleUpload = async () => {
    if (!file) return;
    setLoading(true);
    setResult(null);
    setError(null);
    try {
      const data = await uploadFile(file);
      setResult(data);
    } catch (err) {
      setError(err.response?.data ?? 'No se pudo subir el archivo.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>
        Subir padrón de deudores
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Seleccioná el archivo del BCRA (deudores.txt). El procesamiento es asíncrono:
        la API responde de inmediato y el trabajo se realiza en segundo plano.
      </Typography>

      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
        <Button variant="outlined" component="label" startIcon={<UploadFileIcon />}>
          Elegir archivo
          <input type="file" hidden onChange={handleFileChange} />
        </Button>
        <Typography variant="body2">
          {file ? file.name : 'Ningún archivo seleccionado'}
        </Typography>
      </Stack>

      <Button
        variant="contained"
        onClick={handleUpload}
        disabled={!file || loading}
      >
        {loading ? 'Subiendo...' : 'Subir'}
      </Button>

      {result && (
        <Alert severity="success" sx={{ mt: 2 }}>
          <Typography variant="body2">
            Archivo recibido. Se está procesando en segundo plano.
          </Typography>
          <Box sx={{ mt: 1, fontFamily: 'monospace' }}>
            Import Job ID: {result.importJobId}
          </Box>
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {typeof error === 'string' ? error : JSON.stringify(error)}
        </Alert>
      )}
    </Paper>
  );
}
