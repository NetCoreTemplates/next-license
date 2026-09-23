import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'License agreement',
  description: 'The license terms in force for new Acme Studio purchases.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
