import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import CardContent from '@mui/material/CardContent'
import PageHeader from 'src/@core/components/page-header'

const projects = [
  {
    title: 'Nihome Residence Refresh',
    status: 'In review',
    detail: 'Design coordination placeholder for the new starter-kit-based client workspace.'
  },
  {
    title: 'Sales Gallery Update',
    status: 'Preparing brief',
    detail: 'A sample project card now stands in for future backend-fed project records.'
  },
  {
    title: 'Operations Dashboard Rollout',
    status: 'Planning',
    detail: 'Tracks the first pass of admin baseline adoption before deeper module work begins.'
  }
]

export default function ProjectsPage() {
  return (
    <Grid container spacing={6}>
      <PageHeader
        title={<Typography variant='h4'>Projects</Typography>}
        subtitle={
          <Typography variant='body1' color='text.secondary'>
            These cards replace demo pages with a Nihome-specific project surface that still stays safely placeholder.
          </Typography>
        }
      />

      {projects.map(project => (
        <Grid item xs={12} md={6} xl={4} key={project.title}>
          <Card sx={{ height: '100%' }}>
            <CardContent sx={{ p: 6 }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 3, mb: 3 }}>
                <Typography variant='h6'>{project.title}</Typography>
                <Chip color='primary' label={project.status} size='small' />
              </Box>
              <Typography color='text.secondary'>{project.detail}</Typography>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}
