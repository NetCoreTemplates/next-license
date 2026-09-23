import Link from 'next/link'
import {
  ArrowRight, ArrowUpRight, Check, Command, Download, FileCode2, Folder,
  Infinity as InfinityIcon, LockKeyhole, ShieldCheck, WifiOff, Zap,
} from 'lucide-react'
import Layout from '@/components/layout'

const benefits = [
  { icon: Zap, title: 'Make room for good work', body: 'A free edition to get started, with Pro features ready the moment you need more.' },
  { icon: WifiOff, title: 'Go beautifully offline', body: 'Your paid features are verified on your own device. No connection needed to keep working.' },
  { icon: ShieldCheck, title: 'Buy with a longer view', body: 'One purchase. Perpetual use. Renew for newer Pro features only when it suits you.' },
]

const sidebarItems = [
  { glyph: '◈', label: 'Overview' },
  { glyph: '⌘', label: 'Workspace' },
  { glyph: '↗', label: 'Exports' },
  { glyph: '◇', label: 'License' },
]

// An illustrative release history for the coverage diagram. Order matters:
// builds are drawn by release date, left to right, around the update cutoff.
const coveredBuilds = ['1.0', '1.1', '1.2']
const laterBuilds = ['1.3', '1.4']

const codeLines: (React.ReactNode | null)[] = [
  <span className="code-muted" key="c">{'// A little focus goes a long way.'}</span>,
  null,
  <span key="1"><b>const</b> workspace = {'{'}</span>,
  <span key="2">&nbsp; name: <strong>&apos;My next big idea&apos;</strong>,</span>,
  <span key="3">&nbsp; possibilities: <strong>&apos;endless&apos;</strong>,</span>,
  <span key="4">&nbsp; distractions: <b>0</b>,</span>,
  <span key="5">{'}'};</span>,
  null,
  <span key="6"><b>await</b> create(workspace);<i className="code-cursor"/></span>,
]

export default function Home() {
  return (
    <Layout>
      <section className="studio-hero">
        <div className="hero-grid" aria-hidden="true"/>
        <div className="hero-glow" aria-hidden="true"/>
        <div className="hero-inner">
          <div className="hero-copy">
            <span className="eyebrow-pill"><span className="status-dot"/> Your next favourite developer tool</span>
            <h1>Make great things.<br/><span>Keep them yours.</span></h1>
            <p>A focused workspace for your next idea, and a perpetual Pro license for the long run. Build on your terms, online or off.</p>
            <div className="hero-actions">
              <Link className="studio-button primary" href="/download">Get Acme Studio <Download size={17}/></Link>
              <Link className="studio-button secondary" href="/pricing">Find your license <ArrowRight size={17}/></Link>
            </div>
            <div className="hero-assurances">
              <span><Check size={14}/> Free to get started</span>
              <span><Check size={14}/> One-time purchase</span>
              <span><Check size={14}/> Yours forever</span>
            </div>
          </div>

          <div className="hero-visual">
            <div className="studio-preview" aria-label="Illustrative Acme Studio workspace preview">
            <div className="preview-toolbar">
              <div className="window-dots"><i/><i/><i/></div>
              <span><Command size={12}/> Acme Studio</span>
              <span className="preview-badge">Workspace preview</span>
            </div>
            <div className="preview-body">
              <aside className="preview-sidebar">
                <span className="tiny-label">Your workspace</span>
                <div className="project-label"><Folder size={14}/> My next big idea</div>
                {sidebarItems.map(({ glyph, label }, i) => (
                  <div key={label} className={`preview-menu ${i === 1 ? 'selected' : ''}`}><span>{glyph}</span>{label}</div>
                ))}
                <div className="preview-local"><span className="status-dot"/> Saved locally</div>
              </aside>
              <div className="preview-editor">
                <div className="editor-tabs">
                  <span><FileCode2 size={13}/> project.ts</span>
                  <span>README.md</span>
                </div>
                <div className="code-area">
                  {codeLines.map((line, i) => (
                    <div className="code-line" key={i}>
                      <em>{String(i + 1).padStart(2, '0')}</em>{line}
                    </div>
                  ))}
                </div>
                <div className="preview-output">
                  <span className="tiny-label">Your license at work</span>
                  <div>
                    <span className="output-icon"><ShieldCheck size={20}/></span>
                    <section>
                      <strong>Pro export, unlocked.</strong>
                      <p>Included in your license. Even offline.</p>
                    </section>
                    <span className="output-check"><Check size={14}/></span>
                  </div>
                </div>
              </div>
            </div>
            <div className="preview-status">
                <span><span className="status-dot"/> Ready when you are</span>
                <span>Local workspace <WifiOff size={12}/></span>
              </div>
            </div>

            <div className="ownership-note">
              <span className="ownership-icon"><InfinityIcon size={22}/></span>
              <div>
                <strong>A license. Not a subscription.</strong>
                <p>Your work stays yours. So do your paid features.</p>
              </div>
              <ArrowUpRight size={18}/>
            </div>
          </div>
        </div>
      </section>

      <section className="platform-strip">
        <span>Built for your desktop</span>
        <div>macOS <i/> Windows <i/> Linux</div>
        <span>One workspace. Your way.</span>
      </section>

      <section className="studio-section">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Less friction. More flow.</p>
            <h2>Tools should get out of the way.<br/><span>And stay in your hands.</span></h2>
          </div>
          <p>Choose software that respects your focus, your connection, and your investment.</p>
        </div>
        <div className="benefit-grid">
          {benefits.map(({ icon: Icon, title, body }) => (
            <article className="benefit-card" key={title}>
              <span className="benefit-icon"><Icon size={20}/></span>
              <h3>{title}</h3>
              <p>{body}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="ownership-section">
        <div className="ownership-explainer">
          <p className="eyebrow">A better kind of forever</p>
          <h2>New build.<br/>Same ownership.</h2>
          <p>A dated license unlocks Pro in every build released by your update cutoff. Keep using covered versions forever. Renew for newer builds, or choose lifetime updates.</p>
          <Link href="/docs" className="text-link">See how your license works <ArrowRight size={16}/></Link>
        </div>

        <div className="entitlement-card">
          <div className="entitlement-title">
            <span className="tiny-label">Illustrative dated license</span>
            <span className="soft-badge">Perpetual</span>
          </div>
          <h3>Your version, covered.</h3>
          <ol className="coverage-timeline"
              aria-label="Builds v1.0 to v1.2 are released before the update cutoff and stay unlocked. Builds v1.3 and later need a renewal.">
            {coveredBuilds.map((v, i) => (
              <li key={v} className="covered" style={{ '--i': i } as React.CSSProperties}><i/><span>v{v}</span></li>
            ))}
            <li className="cutoff" aria-hidden="true"><b/><span>Cutoff</span></li>
            {laterBuilds.map(v => (
              <li key={v} className="later"><i/><span>v{v}</span></li>
            ))}
          </ol>
          <div className="entitlement-row">
            <span><Check size={16}/> Earlier app version</span><strong>Still yours</strong>
          </div>
          <div className="entitlement-row">
            <span><Check size={16}/> Build at your cutoff</span><strong>Still yours</strong>
          </div>
          <div className="entitlement-row future">
            <span><LockKeyhole size={15}/> Newer app version</span><strong>Renew to unlock</strong>
          </div>
        </div>
      </section>

      <section className="closing-section">
        <p className="eyebrow">Your next chapter starts here</p>
        <h2>Good work deserves<br/>a great place to happen.</h2>
        <Link href="/download" className="studio-button primary">Explore Acme Studio <ArrowRight size={17}/></Link>
        <p>Start free. Upgrade when you’re ready.</p>
      </section>
    </Layout>
  )
}
