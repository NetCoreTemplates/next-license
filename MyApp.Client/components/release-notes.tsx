import MarkdownContent from './markdown-content'
export default function ReleaseNotes({ children }: { children?: string }) {
  return <MarkdownContent>{children || 'No release notes supplied.'}</MarkdownContent>
}
