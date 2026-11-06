import {
    AppBar,
    Box,
    Container,
    Tab,
    Tabs,
    Toolbar,
    Typography,
} from '@mui/material';
import { useState } from 'react';
import DebtorsTab from './components/DebtorsTab';
import EntitiesTab from './components/EntitiesTab';
import UploadTab from './components/UploadTab';

const TABS = [
  { label: 'Subir archivo', render: () => <UploadTab /> },
  { label: 'Deudores', render: () => <DebtorsTab /> },
  { label: 'Entidades', render: () => <EntitiesTab /> },
];

export default function App() {
  const [tab, setTab] = useState(0);

  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Box
            component="img"
            src="/favicon.ico"
            alt="Wayni"
            sx={{ height: 28, width: 28, mr: 1.5 }}
          />
          <Typography variant="h6">Wayni BCRA — Padrón de deudores</Typography>
        </Toolbar>
      </AppBar>

      <Container maxWidth="md" sx={{ py: 3 }}>
        <Tabs
          value={tab}
          onChange={(_, value) => setTab(value)}
          sx={{ mb: 3 }}
        >
          {TABS.map((t) => (
            <Tab key={t.label} label={t.label} />
          ))}
        </Tabs>

        <Box>{TABS[tab].render()}</Box>
      </Container>
    </>
  );
}
