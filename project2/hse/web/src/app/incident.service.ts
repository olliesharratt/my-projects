import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export type Severity = 'Low' | 'Medium' | 'High' | 'Critical';
export type Status = 'Reported' | 'UnderReview' | 'ActionsAssigned' | 'Closed';
export interface Incident {
  id: string; title: string; description: string; category: string; severity: Severity;
  location: string; reportedBy: string; reportedAt: string; status: Status;
  actions: { id: string; description: string; owner: string; due: string; done: boolean }[];
  audit: { at: string; by: string; event: string }[];
}
export interface Analytics { total: number; bySeverity: Record<string, number>; byMonth: Record<string, number>; overdueActions: number; }

@Injectable({ providedIn: 'root' })
export class IncidentService {
  private http = inject(HttpClient);
  private base = 'http://localhost:5000/api';
  list(status?: string) { return this.http.get<Incident[]>(`${this.base}/incidents`, { params: status ? { status } : {} }); }
  create(body: Partial<Incident>) { return this.http.post<Incident>(`${this.base}/incidents`, body); }
  transition(id: string, to: Status, by: string) { return this.http.post<Incident>(`${this.base}/incidents/${id}/transition`, { to, by }); }
  analytics() { return this.http.get<Analytics>(`${this.base}/analytics`); }
}
