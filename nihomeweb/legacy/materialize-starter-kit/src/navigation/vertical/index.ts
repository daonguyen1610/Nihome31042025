// ** Type import
import { VerticalNavItemsType } from 'src/@core/layouts/types'

const navigation = (): VerticalNavItemsType => {
  return [
    {
      sectionTitle: 'Workspace'
    },
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
      sectionTitle: 'Admin'
    },
    {
      title: 'Admin Entry',
      path: '/admin',
      icon: 'mdi:shield-home-outline'
    },
    {
      title: 'Admin Dashboard',
      path: '/admin/dashboard',
      icon: 'mdi:monitor-dashboard'
    }
  ]
}

export default navigation
