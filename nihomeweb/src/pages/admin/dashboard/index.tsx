import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'
import CardContent from '@mui/material/CardContent'
import PageHeader from 'src/@core/components/page-header'

const dashboardCards = [
  {
    title: 'Admin shell adopted',
    tone: 'success' as const,
    detail: 'Nihome now works on top of the starter-kit layout, theme, navigation, and auth scaffolding.'
  },
  {
    title: 'Framework modernization deferred',
    tone: 'warning' as const,
    detail: 'The team chose speed first, so Next 13 Pages Router is now the official baseline until a later migration.'
  },
  {
    title: 'Full template remains selective',
    tone: 'info' as const,
    detail: 'Future pages can pull compatible components from the larger template one screen at a time.'
  }
]

export default function AdminDashboardPage() {
  return (
    <Grid container spacing={6}>
      <PageHeader
        title={<Typography variant='h4'>Admin dashboard</Typography>}
        subtitle={
          <Typography variant='body1' color='text.secondary'>
            This is the first official Nihome admin screen on top of the starter-kit baseline.
          </Typography>
        }
      />

      {dashboardCards.map(card => (
        <Grid item xs={12} md={6} xl={4} key={card.title}>
          <Card sx={{ height: '100%' }}>
            <CardContent sx={{ p: 6 }}>
              <Chip color={card.tone} label={card.title} sx={{ mb: 3, borderRadius: 2 }} />
              <Typography color='text.secondary'>{card.detail}</Typography>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
