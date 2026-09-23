'use client'

import { Suspense } from "react"
import Link from "next/link"
import { MailCheck } from "lucide-react"
import { useSearchParams } from "next/navigation"
import Layout from "@/components/layout"

function SignUpConfirmContent() {
    const searchParams = useSearchParams()
    const confirmLink = searchParams.get('confirmLink')

    return (
        <div className="auth-page">
            <section className="auth-card">
                <p className="eyebrow">One step left</p>
                <h1>Check your email</h1>
                <p>We sent a confirmation link to the address you registered. Open it to activate your account, then sign in.</p>
                {confirmLink && (
                    <div className="studio-alert warn" style={{ marginTop: 22 }}>
                        <MailCheck size={18}/>
                        <span>
                            Email delivery is not configured in development.{' '}
                            <a id="confirm-link" href={confirmLink}>Confirm this account now</a>.
                        </span>
                    </div>
                )}
                <p className="auth-footer">
                    Already confirmed? <Link className="text-link" href="/signin">Sign in</Link>
                </p>
            </section>
        </div>
    )
}

export default function SignUpConfirm() {
    return (
        <Layout>
            <Suspense fallback={<div className="auth-page"><div className="auth-card"><div className="skeleton" style={{height:180}}/></div></div>}>
                <SignUpConfirmContent />
            </Suspense>
        </Layout>
    )
}
