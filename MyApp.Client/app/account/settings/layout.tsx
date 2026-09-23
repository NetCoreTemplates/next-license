import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Account settings',
  description: 'Email preferences, license transfers and personal data.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
