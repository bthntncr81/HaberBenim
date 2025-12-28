import { Routes } from '@angular/router';

export const routes: Routes = [
  { 
    path: '', 
    loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent) 
  },
  { 
    path: 'news/:id', 
    loadComponent: () => import('./pages/news-detail/news-detail.component').then(m => m.NewsDetailComponent) 
  }
];
