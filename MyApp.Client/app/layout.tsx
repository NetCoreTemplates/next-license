import "../styles/index.css"
import type { Metadata } from 'next'
import { Familjen_Grotesk, Instrument_Sans, JetBrains_Mono } from 'next/font/google'
import Providers from './providers'

// Three roles, three faces: a display grotesk for headlines, a crisp sans for
// everything a person wrote, and a mono reserved for machine-verifiable facts
// — versions, update cutoffs, key suffixes, file sizes, SKUs.
const display = Familjen_Grotesk({ subsets: ['latin'], display: 'swap', variable: '--font-familjen' })
const sans = Instrument_Sans({ subsets: ['latin'], display: 'swap', variable: '--font-instrument' })
const mono = JetBrains_Mono({ subsets: ['latin'], display: 'swap', variable: '--font-jetbrains' })

export const metadata: Metadata = {
  metadataBase: new URL('https://example.org'),
  title: {
    default: 'Acme Studio — software you own',
    template: '%s · Acme Studio',
  },
  description: 'Perpetual desktop software licenses. Buy once, keep your paid features forever, verified offline on your own machine.',
  applicationName: 'Acme Studio',
  openGraph: {
    type: 'website',
    siteName: 'Acme Studio',
    title: 'Acme Studio — software you own',
    description: 'Perpetual desktop software licenses, verified offline. One purchase, perpetual use.',
  },
  twitter: { card: 'summary_large_image' },
  icons: {
    icon: [
      { url: '/favicon/favicon-32x32.png', sizes: '32x32', type: 'image/png' },
      { url: '/favicon/favicon-16x16.png', sizes: '16x16', type: 'image/png' },
    ],
    apple: '/favicon/apple-touch-icon.png',
    shortcut: '/favicon/favicon.ico',
  },
  manifest: '/favicon/site.webmanifest',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en" className={`h-full ${display.variable} ${sans.variable} ${mono.variable}`} suppressHydrationWarning>
      <head>
        {/* Resolve the theme before first paint so the page never flashes light. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `
              (function() {
                try {
                  var theme = localStorage.getItem('color-scheme')
                    || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
                  if (theme === 'dark') document.documentElement.classList.add('dark');
                } catch (e) {}
              })();
            `,
          }}
        />
      </head>
      <body>
        <Providers>{children}</Providers>
      </body>
    </html>
  )
}
