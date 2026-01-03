import { CommonModule } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import {
    NavigationEnd,
    Router,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
} from "@angular/router";
import { filter } from "rxjs";
import { AuthService } from "./services/auth.service";

interface NavItem {
  path: string;
  label: string;
  icon: string;
  svgIcon?: string; // Optional SVG icon path
  roles: string[];
}

@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.scss",
})
export class AppComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  title = "Haber Benim Admin";

  // All navigation items with required roles
  private allNavItems: NavItem[] = [
    {
      path: "/feed",
      label: "Feed",
      icon: "📰",
      roles: ["Admin", "Editor", "SocialMedia"],
    },
    {
      path: "/approval",
      label: "Approval",
      icon: "✅",
      roles: ["Admin", "Editor", "SocialMedia"],
    },
    {
      path: "/breaking",
      label: "Breaking",
      icon: "🔴",
      roles: ["Admin", "Editor"],
    },
    {
      path: "/publishing",
      label: "Publishing",
      icon: "🚀",
      roles: ["Admin", "Editor"],
    },
    {
      path: "/ready-queue",
      label: "Ready Queue",
      icon: "📤",
      roles: ["Admin", "Editor"],
    },
    {
      path: "/emergency-queue",
      label: "Emergency",
      icon: "🚨",
      roles: ["Admin", "Editor"],
    },
    {
      path: "/publishing-settings",
      label: "Publish Settings",
      icon: "⚙️",
      roles: ["Admin"],
    },
    {
      path: "/analytics",
      label: "Analytics",
      icon: "📊",
      roles: ["Admin", "Editor"],
    },
    {
      path: "/alerts",
      label: "Alerts",
      icon: "🔔",
      roles: ["Admin", "Editor"],
    },
    { path: "/reports", label: "Reports", icon: "📋", roles: ["Admin"] },
    { path: "/audit", label: "Audit", icon: "📜", roles: ["Admin"] },
    {
      path: "/sources",
      label: "Sources",
      icon: "🔗",
      roles: ["Admin", "Editor", "SocialMedia"],
    },
    { path: "/rules", label: "Rules", icon: "⚙️", roles: ["Admin"] },
    {
      path: "/integrations/x",
      label: "X Integration",
      icon: "𝕏",
      roles: ["Admin"],
    },
    {
      path: "/integrations/instagram",
      label: "Instagram",
      icon: "",
      svgIcon: "assets/icons/instagram.svg",
      roles: ["Admin"],
    },
    {
      path: "/integrations/openai",
      label: "OpenAI Config",
      icon: "",
      svgIcon: "assets/icons/openai.svg",
      roles: ["Admin"],
    },
    {
      path: "/templates",
      label: "Templates",
      icon: "🎨",
      roles: ["Admin"],
    },
  ];

  // Reactive signals
  isLoggedIn = signal(this.authService.isLoggedIn());
  isLoginPage = signal(false);
  userName = signal(this.authService.getUser()?.displayName ?? "");
  userRoles = signal<string[]>(this.authService.getRoles());

  // Computed filtered nav items based on user roles
  navItems = computed(() => {
    const roles = this.userRoles();
    return this.allNavItems.filter((item) =>
      item.roles.some((role) => roles.includes(role))
    );
  });

  constructor() {
    // Subscribe to auth state changes
    this.authService.getAuthState().subscribe((state) => {
      this.isLoggedIn.set(!!state);
      this.userName.set(state?.user?.displayName ?? "");
      this.userRoles.set(state?.user?.roles ?? []);
    });

    // Track if we're on the login page
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event) => {
        const navEnd = event as NavigationEnd;
        this.isLoginPage.set(navEnd.url.startsWith("/login"));
      });
  }

  logout(): void {
    this.authService.logout();
  }

  getUserInitial(): string {
    const name = this.userName();
    return name ? name.charAt(0).toUpperCase() : "?";
  }
}
