import Link from 'next/link'
import Grid from '@mui/material/Grid'
import Box from '@mui/material/Box'
import Card from '@mui/material/Card'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Divider from '@mui/material/Divider'
import Typography from '@mui/material/Typography'
import CardContent from '@mui/material/CardContent'
import PageHeader from 'src/@core/components/page-header'

const routeCards = [
  {
    title: 'Client projects',
    description: 'Starter-kit cards now anchor the Nihome project workspace instead of demo pages.',
    href: '/projects',
    action: 'Open projects'
  },
  {
    title: 'Client notifications',
    description: 'Notification workflows have a first-pass surface ready for later integrations.',
    href: '/notifications',
    action: 'Review notifications'
  },
  {
    title: 'Admin dashboard',
    description: 'The admin shell is officially based on the imported starter-kit layout system.',
    href: '/admin/dashboard',
    action: 'Go to admin'
  }
]

export default function WorkspaceOverviewPage() {
  return (
    <Grid container spacing={6}>
      <PageHeader
        title={
          <Box>
            <Chip color='primary' label='Nihome baseline reset' sx={{ mb: 3, borderRadius: 2 }} />
            <Typography variant='h4' sx={{ mb: 2 }}>
              The starter-kit is now the working baseline for Nihome.
            </Typography>
          </Box>
        }
        subtitle={
          <Typography variant='body1' color='text.secondary'>
            This overview replaces the old Next 16 placeholder shell with a Pages Router and MUI workspace that we can
            build on immediately while borrowing compatible pieces from the full template later.
          </Typography>
        }
      />

      {routeCards.map(card => (
        <Grid item xs={12} md={6} xl={4} key={card.href}>
          <Card sx={{ height: '100%' }}>
            <CardContent sx={{ p: 6 }}>
              <Typography variant='h6' sx={{ mb: 2 }}>
                {card.title}
              </Typography>
              <Typography color='text.secondary' sx={{ mb: 5 }}>
                {card.description}
              </Typography>
              <Button component={Link} href={card.href} variant='contained'>
                {card.action}
              </Button>
            </CardContent>
          </Card>
        </Grid>
      ))}

      <Grid item xs={12}>
        <Card>
          <CardContent sx={{ p: 6 }}>
            <Stack
              direction={{ xs: 'column', md: 'row' }}
              spacing={4}
              divider={<Divider flexItem orientation='vertical' />}
            >
              <Box sx={{ flex: 1 }}>
                <Typography variant='h6' sx={{ mb: 2 }}>
                  What changed
                </Typography>
                <Typography color='text.secondary'>
                  `nihomeweb` now follows the starter-kit stack on purpose: Next 13 Pages Router, MUI, Emotion, and the
                  imported admin layout system.
                </Typography>
              </Box>
              <Box sx={{ flex: 1 }}>
                <Typography variant='h6' sx={{ mb: 2 }}>
                  What stayed deferred
                </Typography>
                <Typography color='text.secondary'>
                  Real auth, API integration, and feature-depth decisions still wait for later phases. The current auth
                  flow remains mock-backed and clearly marked as placeholder behavior.
                </Typography>
              </Box>
            </Stack>
          </CardContent>
        </Card>
      </Grid>
    </Grid>
  )
}
