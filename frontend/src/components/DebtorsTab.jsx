import {
    Alert,
    Box,
    Button,
    Divider,
    Paper,
    Stack,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    TextField,
    Typography,
} from '@mui/material';
import { useState } from 'react';
import { getDebtor, getTopDebtors } from '../api/debtors';
import { describeCreditStatus } from '../domain/creditStatus';
import { AMOUNT_LABEL, formatAmount } from '../domain/format';

export default function DebtorsTab() {
  // --- Single debtor lookup ---
  const [identification, setIdentification] = useState('');
  const [debtor, setDebtor] = useState(null);
  const [debtorError, setDebtorError] = useState(null);
  const [debtorLoading, setDebtorLoading] = useState(false);

  // --- Top N ---
  const [n, setN] = useState('10');
  const [topDebtors, setTopDebtors] = useState(null);
  const [topError, setTopError] = useState(null);
  const [topLoading, setTopLoading] = useState(false);

  const handleSearchDebtor = async () => {
    if (!identification.trim()) return;
    setDebtorLoading(true);
    setDebtor(null);
    setDebtorError(null);
    try {
      const data = await getDebtor(identification.trim());
      setDebtor(data);
    } catch (err) {
      setDebtorError(
        err.response?.status === 404
          ? 'No se encontró un deudor con esa identificación.'
          : 'Ocurrió un error al consultar el deudor.',
      );
    } finally {
      setDebtorLoading(false);
    }
  };

  const handleSearchTop = async () => {
    const parsed = Number(n);
    if (!Number.isInteger(parsed) || parsed <= 0) {
      setTopError('Ingresá un número entero mayor a 0.');
      return;
    }
    setTopLoading(true);
    setTopDebtors(null);
    setTopError(null);
    try {
      const data = await getTopDebtors(parsed);
      setTopDebtors(data);
    } catch {
      setTopError('Ocurrió un error al consultar el ranking.');
    } finally {
      setTopLoading(false);
    }
  };

  return (
    <Stack spacing={3}>
      {/* Single debtor */}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Consultar deudor
        </Typography>
        <Stack
          component="form"
          onSubmit={(e) => {
            e.preventDefault();
            handleSearchDebtor();
          }}
          direction="row"
          spacing={2}
          sx={{ mb: 2 }}
        >
          <TextField
            label="Número de identificación"
            value={identification}
            onChange={(e) => setIdentification(e.target.value)}
            size="small"
            fullWidth
          />
          <Button type="submit" variant="contained" disabled={debtorLoading}>
            Buscar
          </Button>
        </Stack>

        {debtor && (
          <Box sx={{ mt: 1 }}>
            <Typography variant="body2">
              <strong>Identificación:</strong> {debtor.nro_identificacion}
            </Typography>
            <Typography variant="body2">
              <strong>Situación máxima:</strong>{' '}
              {describeCreditStatus(debtor.situacion_maxima)}
            </Typography>
            <Typography variant="body2">
              <strong>{AMOUNT_LABEL}:</strong>{' '}
              {formatAmount(debtor.suma_total_prestamos)}
            </Typography>
          </Box>
        )}
        {debtorError && (
          <Alert severity="warning" sx={{ mt: 1 }}>
            {debtorError}
          </Alert>
        )}
      </Paper>

      {/* Top N */}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Top deudores por financiación
        </Typography>
        <Stack
          component="form"
          onSubmit={(e) => {
            e.preventDefault();
            handleSearchTop();
          }}
          direction="row"
          spacing={2}
          sx={{ mb: 2 }}
        >
          <TextField
            label="N"
            type="number"
            value={n}
            onChange={(e) => setN(e.target.value)}
            size="small"
            sx={{ width: 120 }}
          />
          <Button type="submit" variant="contained" disabled={topLoading}>
            Buscar
          </Button>
        </Stack>

        {topError && <Alert severity="warning">{topError}</Alert>}

        {topDebtors && topDebtors.length > 0 && (
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Identificación</TableCell>
                  <TableCell>Situación máxima</TableCell>
                  <TableCell align="right">{AMOUNT_LABEL}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {topDebtors.map((d, index) => (
                  <TableRow key={d.nro_identificacion}>
                    <TableCell>{index + 1}</TableCell>
                    <TableCell>{d.nro_identificacion}</TableCell>
                    <TableCell>{describeCreditStatus(d.situacion_maxima)}</TableCell>
                    <TableCell align="right">
                      {formatAmount(d.suma_total_prestamos)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
        {topDebtors && topDebtors.length === 0 && (
          <>
            <Divider sx={{ my: 1 }} />
            <Typography variant="body2" color="text.secondary">
              No hay deudores para mostrar.
            </Typography>
          </>
        )}
      </Paper>
    </Stack>
  );
}
