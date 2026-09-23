import Link from 'next/link'
import { ShieldAlert } from 'lucide-react'
import Layout from '@/components/layout'

export const metadata = { title: 'No access' }

export default function Forbidden() {
  return (
    <Layout>
      <div className="studio-page narrow">
        <div className="studio-empty">
          <ShieldAlert size={28}/>
          <p className="eyebrow">403</p>
          <h1 style={{ fontSize: 'var(--t-xl)' }}>This page needs different access</h1>
          <p>Your account is signed in but is not permitted here. If this is your software business, sign in with the operator account.</p>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', justifyContent: 'center' }}>
            <Link href="/account" className="studio-button primary">Go to my licenses</Link>
            <Link href="/" className="studio-button secondary">Back to home</Link>
          </div>
        </div>
      </div>
    </Layout>
  )
}
