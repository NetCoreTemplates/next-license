import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

/** Render untrusted Markdown without executing embedded HTML. */
export default function MarkdownContent({ children }: { children?: string }) {
  return <div className="release-notes"><ReactMarkdown remarkPlugins={[remarkGfm]} skipHtml>{children ?? ''}</ReactMarkdown></div>
}
