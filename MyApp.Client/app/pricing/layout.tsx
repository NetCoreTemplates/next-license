import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Pricing',
  description: 'One-time purchase, perpetual use. Choose 12 months of Pro updates or lifetime coverage.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
