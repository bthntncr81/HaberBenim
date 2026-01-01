import { Routes } from "@angular/router";
import { authGuard } from "./guards/auth.guard";
import { roleGuard } from "./guards/role.guard";

export const routes: Routes = [
  { path: "", redirectTo: "/feed", pathMatch: "full" },
  {
    path: "login",
    loadComponent: () =>
      import("./pages/login/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "feed",
    loadComponent: () =>
      import("./pages/feed/feed.component").then((m) => m.FeedComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor", "SocialMedia"] },
  },
  {
    path: "approval",
    loadComponent: () =>
      import("./pages/approval/approval.component").then(
        (m) => m.ApprovalComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor", "SocialMedia"] },
  },
  {
    path: "editor/:id",
    loadComponent: () =>
      import("./pages/editor/editor.component").then((m) => m.EditorComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor", "SocialMedia"] },
  },
  {
    path: "publishing",
    loadComponent: () =>
      import("./pages/publishing/publishing.component").then(
        (m) => m.PublishingComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor"] },
  },
  {
    path: "sources",
    loadComponent: () =>
      import("./pages/sources/sources.component").then(
        (m) => m.SourcesComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor", "SocialMedia"] }, // All can view, only Admin can edit
  },
  {
    path: "rules",
    loadComponent: () =>
      import("./pages/rules/rules.component").then((m) => m.RulesComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
  {
    path: "analytics",
    loadComponent: () =>
      import("./pages/analytics/analytics.component").then(
        (m) => m.AnalyticsComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor"] },
  },
  {
    path: "reports",
    loadComponent: () =>
      import("./pages/reports/reports.component").then(
        (m) => m.ReportsComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
  {
    path: "breaking",
    loadComponent: () =>
      import("./pages/breaking/breaking.component").then(
        (m) => m.BreakingComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor"] },
  },
  {
    path: "alerts",
    loadComponent: () =>
      import("./pages/alerts/alerts.component").then((m) => m.AlertsComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin", "Editor"] },
  },
  {
    path: "audit",
    loadComponent: () =>
      import("./pages/audit/audit.component").then((m) => m.AuditComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
  {
    path: "integrations/x",
    loadComponent: () =>
      import("./pages/x-integration/x-integration.component").then(
        (m) => m.XIntegrationComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
  {
    path: "integrations/instagram",
    loadComponent: () =>
      import(
        "./pages/instagram-integration/instagram-integration.component"
      ).then((m) => m.InstagramIntegrationComponent),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
  {
    path: "integrations/openai",
    loadComponent: () =>
      import("./pages/openai-config/openai-config.component").then(
        (m) => m.OpenAiConfigComponent
      ),
    canActivate: [authGuard, roleGuard],
    data: { roles: ["Admin"] },
  },
];
