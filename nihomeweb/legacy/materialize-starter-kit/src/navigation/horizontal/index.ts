// ** Type import
import { HorizontalNavItemsType } from 'src/@core/layouts/types'

const navigation = (): HorizontalNavItemsType => [
  {
    title: 'Overview',
    path: '/',
    icon: 'mdi:view-dashboard-outline'
  },
  {
    title: 'Projects',
    path: '/projects',
    icon: 'mdi:briefcase-outline'
  },
  {
    title: 'Notifications',
    path: '/notifications',
    icon: 'mdi:bell-outline'
  },
  {
    title: 'Admin Dashboard',
    path: '/admin/dashboard',
    icon: 'mdi:monitor-dashboard'
  }
]

export default navigation
