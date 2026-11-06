import UploadFileIcon from '@mui/icons-material/UploadFile';
import {
    Alert,
    Box,
    Button,
    LinearProgress,
    Paper,
    Stack,
    Typography
} from '@mui/material';
import { useState } from 'react';
import { uploadFile } from '../api/imports';

export default function UploadTab() {
  const [file, setFile] = useState(null);
  const [loading, setLoading] = useState(false);
  const [progress, setProgress] = useState(0);
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
    setProgress(0);
    setResult(null);
    setError(null);
    try {
      const data = await uploadFile(file, setProgress);
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

      <Stack direction="row" spacing={2} sx={{ mb: 2, alignItems: 'center' }}>
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

      {loading && (
        <Box sx={{ mt: 2 }}>
          <LinearProgress variant="determinate" value={progress} />
          <Typography variant="caption" color="text.secondary">
            Subiendo archivo: {progress}%
          </Typography>
        </Box>
      )}

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
