import type { Metadata } from 'next'

export const metadata: Metadata = {
  title: 'Changelog',
  description: 'Release notes for every published build, with the release date your license coverage is measured against.',
}

export default function SectionLayout({ children }: { children: React.ReactNode }) {
  return children
}
