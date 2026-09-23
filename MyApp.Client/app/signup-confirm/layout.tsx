import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Confirm your email',
  description: 'Confirm your email address to activate your Acme Studio account.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
