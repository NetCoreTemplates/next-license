import Nav from "./nav"
import Footer from "./footer"

type Props = {
  children: React.ReactNode
}

const Layout = ({ children }: Props) => {
  return (
    <>
      <a className="skip-link" href="#main">Skip to content</a>
      <Nav />
      <div className="studio-content">
        <main id="main" tabIndex={-1}>{children}</main>
      </div>
      <Footer />
    </>
  )
}

export default Layout
