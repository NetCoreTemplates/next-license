"use client";
import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ArrowUpRight,
  Box,
  CreditCard,
  Download,
  KeyRound,
  LayoutDashboard,
  Settings2,
  ShoppingBag,
  FileText,
} from "lucide-react";
import Layout from "@/components/layout";
const links = [
  ["", "Overview", LayoutDashboard],
  ["catalog", "Products & pricing", CreditCard],
  ["releases", "Releases", Download],
  ["licenses", "Licenses", KeyRound],
  ["orders", "Orders", ShoppingBag],
  ["agreements", "License terms", FileText],
  ["settings", "Integrations", Settings2],
] as const;
export default function OperationsShell({ children }: { children: ReactNode }) {
  const path = usePathname();
  return (
    <Layout>
      <div className="ops-shell">
        <aside className="ops-sidebar">
          <div className="ops-brand">
            <span>
              <Box size={22} />
            </span>
            <div>
              Studio console<small>Software business</small>
            </div>
          </div>
          <p className="ops-nav-label">Workspace</p>
          <nav aria-label="Operations navigation">
            {links.map(([slug, label, Icon]) => (
              <Link
                key={slug}
                href={`/admin${slug ? "/" + slug : ""}`}
                aria-current={
                  path === `/admin${slug ? "/" + slug : ""}`
                    ? "page"
                    : undefined
                }
              >
                <Icon size={18} />
                {label}
              </Link>
            ))}
          </nav>
          <div className="ops-sidebar-bottom">
            <span><span className="ops-live-dot" /> Your software. Your business.</span>
            <a href="/admin-ui">
              System administration <ArrowUpRight size={14} />
            </a>
          </div>
        </aside>
        <div className="ops-main">{children}</div>
      </div>
    </Layout>
  );
}
export function PageHeading({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <header className="ops-heading">
      <div>
        <p className="ops-eyebrow">{eyebrow ?? "Studio / operations"}</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </header>
  );
}
export function Panel({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return <div className={`ops-panel ${className}`}>{children}</div>;
}
export function StatusPill({
  children,
  tone = "green",
}: {
  children: ReactNode;
  tone?: "green" | "blue" | "amber" | "red" | "slate";
}) {
  return <span className={`ops-pill ${tone}`}>{children}</span>;
}
