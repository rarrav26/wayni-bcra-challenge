import {
    Alert,
    Box,
    Button,
    Paper,
    Stack,
    TextField,
    Typography,
} from '@mui/material';
import { useState } from 'react';
import { getEntity } from '../api/entities';
import { AMOUNT_LABEL, formatAmount } from '../domain/format';

export default function EntitiesTab() {
  const [code, setCode] = useState('');
  const [entity, setEntity] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  const handleSearch = async () => {
    if (!code.trim()) return;
    setLoading(true);
    setEntity(null);
    setError(null);
    try {
      const data = await getEntity(code.trim());
      setEntity(data);
    } catch (err) {
      setError(
        err.response?.status === 404
          ? 'No se encontró una entidad con ese código.'
          : 'Ocurrió un error al consultar la entidad.',
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: 3 }}>
      <Typography variant="h6" gutterBottom>
        Consultar entidad financiera
      </Typography>
      <Stack
        component="form"
        onSubmit={(e) => {
          e.preventDefault();
          handleSearch();
        }}
        direction="row"
        spacing={2}
        sx={{ mb: 2 }}
      >
        <TextField
          label="Código de entidad"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          size="small"
          fullWidth
        />
        <Button type="submit" variant="contained" disabled={loading}>
          Buscar
        </Button>
      </Stack>

      {entity && (
        <Box sx={{ mt: 1 }}>
          <Typography variant="body2">
            <strong>Código de entidad:</strong> {entity.codigo_entidad}
          </Typography>
          <Typography variant="body2">
            <strong>{AMOUNT_LABEL}:</strong>{' '}
            {formatAmount(entity.suma_total_prestamos)}
          </Typography>
        </Box>
      )}
      {error && (
        <Alert severity="warning" sx={{ mt: 1 }}>
          {error}
        </Alert>
      )}
    </Paper>
  );
}
