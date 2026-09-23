import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Downloads',
  description: 'Get the latest Acme Studio build for macOS, Windows or Linux. Start free, unlock Pro with a license.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
