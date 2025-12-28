import { CommonModule } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ReportsApiService } from "../../services/reports-api.service";
import { DailyReportRun } from "../../shared/reports.models";

@Component({
  selector: "app-reports",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./reports.component.html",
  styleUrl: "./reports.component.scss",
})
export class ReportsComponent implements OnInit {
  private api = inject(ReportsApiService);

  // Data
  runs = signal<DailyReportRun[]>([]);

  // Form
  selectedDate = "";
  runsFromDate = "";
  runsToDate = "";

  // UI State
  isLoading = signal(false);
  isGenerating = signal(false);
  isDownloading = signal(false);
  error = signal<string | null>(null);

  // Toast
  toastMessage = signal("");
  toastType = signal<"success" | "error">("success");
  showToast = signal(false);

  ngOnInit(): void {
    // Default selected date to yesterday
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    this.selectedDate = yesterday.toISOString().split("T")[0];

    // Default runs range to last 30 days
    const monthAgo = new Date();
    monthAgo.setDate(monthAgo.getDate() - 30);
    this.runsFromDate = monthAgo.toISOString().split("T")[0];
    this.runsToDate = new Date().toISOString().split("T")[0];

    this.loadRuns();
  }

  loadRuns(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api
      .listRuns({
        from: this.runsFromDate,
        to: this.runsToDate,
      })
      .subscribe({
        next: (data) => {
          this.runs.set(data);
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error("Failed to load report runs", err);
          this.error.set("Failed to load report runs");
          this.isLoading.set(false);
        },
      });
  }

  generateReport(): void {
    if (!this.selectedDate || this.isGenerating()) return;

    this.isGenerating.set(true);

    this.api.generateDaily(this.selectedDate).subscribe({
      next: (result) => {
        this.isGenerating.set(false);
        if (result.success) {
          this.showToastMessage(
            `Report generated with ${result.itemCount} items`,
            "success"
          );
          this.loadRuns();
        } else {
          this.showToastMessage(
            result.error || "Failed to generate report",
            "error"
          );
        }
      },
      error: (err) => {
        this.isGenerating.set(false);
        this.showToastMessage("Failed to generate report", "error");
      },
    });
  }

  downloadReport(date?: string): void {
    const reportDate = date || this.selectedDate;
    if (!reportDate || this.isDownloading()) return;

    this.isDownloading.set(true);

    this.api.downloadDaily(reportDate).subscribe({
      next: (blob) => {
        this.isDownloading.set(false);

        // Create download link
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `daily-report-${reportDate}.xlsx`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);

        this.showToastMessage("Report downloaded", "success");
      },
      error: (err) => {
        this.isDownloading.set(false);
        if (err.status === 404) {
          this.showToastMessage("Report not found for this date", "error");
        } else {
          this.showToastMessage("Failed to download report", "error");
        }
      },
    });
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  getStatusClass(status: string): string {
    return status.toLowerCase();
  }

  private showToastMessage(message: string, type: "success" | "error"): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}
