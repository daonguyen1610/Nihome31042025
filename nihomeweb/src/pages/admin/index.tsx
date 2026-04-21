export async function getServerSideProps() {
  return {
  redirect: {
    destination: '/admin/dashboard',
    permanent: false
  }
  }
}

export default function AdminEntryPage() {
  return null
}
