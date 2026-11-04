import { bootstrapApplication } from '@angular/platform-browser';
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { signal } from '@angular/core';

interface Asset { id: string; name: string; site: string; type: string; status: 'Online' | 'Warning' | 'Offline' | 'Maintenance'; updatedAt: string; }

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  template: `
    <main class="shell">
      <header><div><p class="eyebrow">OPERATIONS CENTER</p><h1>Asset monitor</h1><p class="muted">Real-time health across your industrial fleet</p></div><button class="user">AS <span>Admin user</span>⌄</button></header>
      <section class="metrics"><article><span>ASSETS ONLINE</span><strong>{{ onlineCount() }}</strong><small>↑ 4.8% from last week</small></article><article><span>ACTIVE ALERTS</span><strong class="danger">8</strong><small class="danger">3 need attention</small></article><article><span>OPEN WORK ORDERS</span><strong>12</strong><small>4 due this week</small></article><article><span>FLEET HEALTH</span><strong>94.2%</strong><small>↑ 2.1% from last week</small></article></section>
      <section class="content"><div class="panel wide"><div class="panel-title"><div><h2>Asset overview</h2><p class="muted">Live status across all sites</p></div><button class="outline">＋ Add asset</button></div><div class="filters"><input placeholder="Search assets..." (input)="search.set($any($event.target).value)"><select><option>All sites</option><option>North Plant</option><option>South Plant</option></select><select><option>All types</option><option>Pump</option><option>Compressor</option><option>Turbine</option></select><select><option>All status</option><option>Online</option><option>Warning</option></select></div><table><thead><tr><th>ASSET</th><th>SITE</th><th>TYPE</th><th>STATUS</th><th>LAST UPDATE</th><th></th></tr></thead><tbody><tr *ngFor="let asset of filteredAssets()"><td><b>{{ asset.name }}</b><small>{{ asset.id.slice(0,8) }}</small></td><td>{{ asset.site }}</td><td>{{ asset.type }}</td><td><span class="status" [class.warning]="asset.status === 'Warning'">● {{ asset.status }}</span></td><td>{{ asset.updatedAt }}</td><td>•••</td></tr></tbody></table></div><div class="panel"><div class="panel-title"><div><h2>Live telemetry</h2><p class="muted">North Plant · Pump P-102</p></div><span class="live">● LIVE</span></div><div class="chart"><div class="chart-line"></div><span>08:00</span><span>10:00</span><span>12:00</span><span>14:00</span><span>16:00</span></div><div class="legend"><span>Temperature</span><span>Vibration</span></div></div></section>
    </main>`,
  styles: [':host{display:block}']
})
class AppComponent {
  private readonly http = inject(HttpClient);
  readonly search = signal('');
  readonly assets = signal<Asset[]>([
    { id: 'P-102-8b1c', name: 'Pump P-102', site: 'North Plant', type: 'Pump', status: 'Online', updatedAt: 'Just now' },
    { id: 'C-204-2a7f', name: 'Compressor C-204', site: 'North Plant', type: 'Compressor', status: 'Warning', updatedAt: '1 min ago' },
    { id: 'T-301-9d4e', name: 'Turbine T-301', site: 'South Plant', type: 'Turbine', status: 'Online', updatedAt: '2 min ago' },
    { id: 'P-118-5c2d', name: 'Pump P-118', site: 'South Plant', type: 'Pump', status: 'Online', updatedAt: '3 min ago' }
  ]);
  readonly filteredAssets = () => this.assets().filter(asset => asset.name.toLowerCase().includes(this.search().toLowerCase()));
  readonly onlineCount = () => this.assets().filter(asset => asset.status === 'Online').length;
}

bootstrapApplication(AppComponent, { providers: [provideHttpClient()] }).catch(error => console.error(error));
