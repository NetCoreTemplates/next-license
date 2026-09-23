import Link from 'next/link'
import { ArrowRight, Boxes, FileKey2, UserCheck, WifiOff } from 'lucide-react'
import Layout from '@/components/layout'
import { PageHeader } from '@/components/page-parts'

export const metadata = {
  title: 'Features',
  description: 'Start free, paste a license key to unlock Pro. No account connection or activation server needed inside the app.',
}

const features = [
  { icon: FileKey2, title: 'Pro export', body: 'Export your work from the desktop workspace in every format the Pro edition includes.' },
  { icon: Boxes, title: 'Batch processing', body: 'Apply one transformation across a whole project in a single step.' },
  { icon: WifiOff, title: 'Offline by default', body: 'The app checks your signed key on your own machine. No connection, no activation server, no check-in.' },
  { icon: UserCheck, title: 'Registered to you', body: 'Your name, organization and licensed seat count are readable inside the app, straight from the license file.' },
]

export default function Features() {
  return (
    <Layout>
      <div className="studio-page">
        <PageHeader
          eyebrow="One key. Pro unlocked."
          title="Simple tools. Simple licensing."
          lead="Start with the free editing workspace. Paste your license key to unlock Pro in every covered app version — no sign-in inside the app, ever."
        />

        <div className="studio-grid two" style={{ marginTop: 40 }}>
          {features.map(({ icon: Icon, title, body }) => (
            <section className="studio-card" key={title}>
              <span className="benefit-icon"><Icon size={20}/></span>
              <h2>{title}</h2>
              <p style={{ marginTop: 8, color: 'var(--ink-muted)', fontSize: 'var(--t-sm)', lineHeight: 1.8 }}>{body}</p>
            </section>
          ))}
        </div>

        <div className="studio-card sunken" style={{ marginTop: 32, display: 'flex', flexWrap: 'wrap', gap: 20, alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <h2 style={{ fontSize: 'var(--t-md)' }}>Ready to unlock Pro?</h2>
            <p style={{ marginTop: 6, color: 'var(--ink-muted)', fontSize: 'var(--t-sm)' }}>One purchase, perpetual use. Renew only when you want newer builds.</p>
          </div>
          <Link href="/pricing" className="studio-button primary">Choose your Pro license <ArrowRight size={16}/></Link>
        </div>
      </div>
    </Layout>
  )
}
