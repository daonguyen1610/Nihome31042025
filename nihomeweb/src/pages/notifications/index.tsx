import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import CardContent from '@mui/material/CardContent'
import PageHeader from 'src/@core/components/page-header'

const notifications = [
  {
    title: 'Starter baseline applied',
    tone: 'success' as const,
    detail: 'The official admin template has shifted from the old custom shell to the imported starter-kit.'
  },
  {
    title: 'Vendor branding removed',
    tone: 'info' as const,
    detail: 'Legacy template branding has been replaced with Nihome wording throughout the active UI.'
  },
  {
    title: 'Full template deferred',
    tone: 'warning' as const,
    detail: 'The larger template stays out of this pass and will be mined selectively for later pages.'
  }
]

export default function NotificationsPage() {
  return (
    <Grid container spacing={6}>
      <PageHeader
        title={<Typography variant='h4'>Notifications</Typography>}
        subtitle={
          <Typography variant='body1' color='text.secondary'>
            A lightweight notification surface for the new baseline, ready to be replaced with live signals later.
          </Typography>
        }
      />

      <Grid item xs={12}>
        <Card>
          <CardContent sx={{ p: 6 }}>
            <Stack spacing={4}>
              {notifications.map(notification => (
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={3} key={notification.title}>
                  <Chip color={notification.tone} label={notification.title} sx={{ alignSelf: 'flex-start' }} />
                  <Typography color='text.secondary'>{notification.detail}</Typography>
                </Stack>
              ))}
            </Stack>
          </CardContent>
        </Card>
      </Grid>
    </Grid>
  )
}
