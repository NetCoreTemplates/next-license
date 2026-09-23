import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Operations',
  description: 'Run your software business: products, releases, licenses, orders and license terms.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
