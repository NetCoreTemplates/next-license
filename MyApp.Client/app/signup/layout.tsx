import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Create an account',
  description: 'Create an account to keep your Acme Studio licenses, orders and account settings in one place.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
