import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Analytics, Incident, IncidentService, Status } from './incident.service';

const NEXT: Record<Status, Status[]> = {
  Reported: ['UnderReview'], UnderReview: ['ActionsAssigned', 'Closed'], ActionsAssigned: ['Closed'], Closed: []
};

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  template: `
  <main>
    <h1>HS&amp;E Incident Reporting</h1>

    @if (stats(); as s) {
      <section aria-label="Summary" class="stats">
        <div>Total: <strong>{{ s.total }}</strong></div>
        <div>Overdue actions: <strong>{{ s.overdueActions }}</strong></div>
        @for (k of keys(s.bySeverity); track k) { <div>{{ k }}: <strong>{{ s.bySeverity[k] }}</strong></div> }
      </section>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" aria-label="Report an incident">
      <label for="title">Title</label>
      <input id="title" formControlName="title" />
      <label for="description">Description</label>
      <textarea id="description" formControlName="description"></textarea>
      <label for="category">Category</label>
      <select id="category" formControlName="category">
        @for (c of categories; track c) { <option>{{ c }}</option> }
      </select>
      <label for="severity">Severity</label>
      <select id="severity" formControlName="severity">
        @for (s of severities; track s) { <option>{{ s }}</option> }
      </select>
      <label for="location">Location</label>
      <input id="location" formControlName="location" />
      <label for="reportedBy">Your name</label>
      <input id="reportedBy" formControlName="reportedBy" />
      <button type="submit" [disabled]="form.invalid">Submit report</button>
    </form>

    <h2>Incidents</h2>
    @for (i of incidents(); track i.id) {
      <article>
        <h3>{{ i.title }} <small>({{ i.severity }} · {{ i.status }})</small></h3>
        <p>{{ i.location }} · reported by {{ i.reportedBy }} on {{ i.reportedAt | date:'medium' }}</p>
        @for (to of next(i.status); track to) {
          <button type="button" (click)="move(i, to)">Move to {{ to }}</button>
        }
        <details>
          <summary>Audit trail ({{ i.audit.length }})</summary>
          <ul>@for (a of i.audit; track a.at) { <li>{{ a.at | date:'short' }} – {{ a.by }}: {{ a.event }}</li> }</ul>
        </details>
      </article>
    } @empty { <p>No incidents yet.</p> }
    @if (error()) { <p role="alert">{{ error() }}</p> }
  </main>`,
  styles: [`main{max-width:760px;margin:2rem auto;font-family:system-ui,sans-serif}
    form{display:grid;gap:.4rem}.stats{display:flex;gap:1rem;flex-wrap:wrap}
    article{border:1px solid #ccc;border-radius:6px;padding:.75rem;margin:.75rem 0}
    [role=alert]{color:#b00020}`]
})
export class AppComponent implements OnInit {
  private svc = inject(IncidentService);
  private fb = inject(FormBuilder);
  incidents = signal<Incident[]>([]);
  stats = signal<Analytics | null>(null);
  error = signal('');
  categories = ['NearMiss', 'Injury', 'Environmental', 'EquipmentFailure', 'Other'];
  severities = ['Low', 'Medium', 'High', 'Critical'];
  form = this.fb.nonNullable.group({
    title: ['', Validators.required], description: [''], category: ['NearMiss'],
    severity: ['Low'], location: ['', Validators.required], reportedBy: ['', Validators.required]
  });
  ngOnInit() { this.refresh(); }
  keys = (o: Record<string, number>) => Object.keys(o);
  next = (s: Status) => NEXT[s];
  refresh() {
    this.svc.list().subscribe(r => this.incidents.set(r));
    this.svc.analytics().subscribe(r => this.stats.set(r));
  }
  submit() {
    this.svc.create(this.form.getRawValue() as any).subscribe(() => { this.form.reset({ category: 'NearMiss', severity: 'Low' }); this.refresh(); });
  }
  move(i: Incident, to: Status) {
    this.error.set('');
    this.svc.transition(i.id, to, 'reviewer').subscribe({ next: () => this.refresh(), error: e => this.error.set(e.error ?? 'Transition failed') });
  }
}
