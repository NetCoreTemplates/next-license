import Link from 'next/link'
import { Command, ArrowUpRight } from 'lucide-react'

const groups: [string, [string, string][]][] = [
  ['Product', [['/features', 'Features'], ['/pricing', 'Pricing'], ['/download', 'Downloads']]],
  ['Resources', [['/docs', 'How licensing works'], ['/changelog', 'Changelog'], ['/account', 'My licenses']]],
  ['The details', [['/eula', 'License agreement'], ['/privacy', 'Privacy']]],
]

export default function Footer() {
  return (
    <footer className="studio-footer">
      <div className="footer-inner">
        <div className="footer-top">
          <div>
            <Link href="/" className="studio-brand">
              <span className="studio-mark"><Command size={18}/></span>
              acme<span className="brand-light">studio</span>
            </Link>
            <p>Good tools. Great work. Yours for the long run.</p>
          </div>
          <nav aria-label="Footer">
            {groups.map(([heading, items]) => (
              <div key={heading}>
                <span>{heading}</span>
                {items.map(([href, label]) => <Link key={href} href={href}>{label}</Link>)}
                {heading === 'The details' && (
                  <a href="https://github.com/NetCoreTemplates/next-license">Source <ArrowUpRight size={12}/></a>
                )}
              </div>
            ))}
          </nav>
        </div>
        <div className="footer-bottom">
          <span>Acme Studio — software you own.</span>
          <span><i className="status-dot"/> Offline by design. Yours forever.</span>
        </div>
      </div>
    </footer>
  )
}
