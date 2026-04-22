import { useEffect } from 'react'
import { useRouter } from 'next/router'

export default function AdminEntryPage() {
  const router = useRouter()

  useEffect(() => {
    void router.replace('/admin/dashboard')
  }, [router])

  return null
}
