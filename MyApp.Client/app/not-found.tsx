import Link from 'next/link'
import { Compass } from 'lucide-react'
import Layout from '@/components/layout'

export const metadata = { title: 'Page not found' }

export default function NotFound() {
  return (
    <Layout>
      <div className="studio-page narrow">
        <div className="studio-empty">
          <Compass size={28}/>
          <p className="eyebrow">404</p>
          <h1 style={{ fontSize: 'var(--t-xl)' }}>That page isn’t here</h1>
          <p>The link may be out of date. These are the places people usually want.</p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', justifyContent: 'center' }}>
            <Link href="/download" className="studio-button primary">Downloads</Link>
            <Link href="/pricing" className="studio-button secondary">Pricing</Link>
            <Link href="/docs" className="studio-button secondary">How licensing works</Link>
          </div>
        </div>
      </div>
    </Layout>
  )
}
