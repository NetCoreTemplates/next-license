'use client'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { ArrowUpRight, Command, Menu, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { appAuth } from '@/lib/auth'
import ThemeToggle from './theme-toggle'

const links = [
  ['/features', 'Features'],
  ['/pricing', 'Pricing'],
  ['/download', 'Downloads'],
  ['/docs', 'Docs'],
]

export default function Nav() {
  const pathname = usePathname()
  const { user, hasRole, signOut } = appAuth()
  const [open, setOpen] = useState(false)

  // The menu closes when the route changes and on Escape, so it never
  // covers the page someone just navigated to.
  useEffect(() => setOpen(false), [pathname])
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false) }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  const current = (href: string) => pathname === href || pathname.startsWith(href + '/') ? 'page' as const : undefined

  return (
    <header className="studio-header">
      <div className="studio-nav">
        <Link href="/" className="studio-brand" aria-label="Acme Studio home">
          <span className="studio-mark"><Command size={18}/></span>
          acme<span className="brand-light">studio</span><span className="brand-dot"/>
        </Link>

        <nav className="desktop-links" aria-label="Main">
          {links.map(([href, title]) => (
            <Link key={href} href={href} aria-current={current(href)}>{title}</Link>
          ))}
        </nav>

        <div className="nav-actions">
          <ThemeToggle/>
          {hasRole('Admin') && (
            <Link href="/admin" className="desktop-only studio-button ghost" aria-current={current('/admin')}>Operations</Link>
          )}
          {user && (
            <button className="desktop-only studio-button ghost" onClick={() => signOut('/')}>Sign out</button>
          )}
          <Link href={user ? '/account' : '/signin'} className="nav-account" aria-current={current('/account')}>
            {user ? 'My licenses' : 'Sign in'}<ArrowUpRight size={14}/>
          </Link>
          <button
            className="mobile-toggle"
            onClick={() => setOpen(!open)}
            aria-label={open ? 'Close menu' : 'Open menu'}
            aria-expanded={open}
            aria-controls="mobile-menu"
          >
            {open ? <X size={20}/> : <Menu size={20}/>}
          </button>
        </div>
      </div>

      {open && (
        <nav className="mobile-links" id="mobile-menu" aria-label="Mobile">
          {links.map(([href, title]) => (
            <Link key={href} href={href} aria-current={current(href)}>{title}</Link>
          ))}
          <Link href={user ? '/account' : '/signin'} aria-current={current('/account')}>
            {user ? 'My licenses' : 'Sign in'}<ArrowUpRight size={15}/>
          </Link>
          {hasRole('Admin') && <Link href="/admin" aria-current={current('/admin')}>Operations</Link>}
          {user && <button onClick={() => signOut('/')}>Sign out</button>}
        </nav>
      )}
    </header>
  )
}
