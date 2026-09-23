import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Sign in',
  description: 'Sign in to manage your Acme Studio licenses.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
