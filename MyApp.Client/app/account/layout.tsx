import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'My licenses',
  description: 'Download your license files, check orders, and manage your account.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
