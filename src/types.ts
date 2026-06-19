export interface JobListing {
  id: string;
  title: string;
  company: string;
  location: string;
  description: string;
  url: string;
  source: 'linkedin' | 'indeed';
  isEasyApply: boolean;
  postedDate?: string;
  salary?: string;
}

export interface MatchResult {
  score: number;
  summary: string;
  reasons: string[];
}

export interface JobMatch {
  job: JobListing;
  match: MatchResult;
  coverLetter?: string;
  applied: boolean;
  applicationStatus?: 'submitted' | 'pending' | 'failed';
}

export interface SearchConfig {
  jobTitle: string;
  location: string;
  keywords: string;
  sites: Array<'linkedin' | 'indeed'>;
  minScore: number;
  maxJobsPerSite: number;
  easyApplyOnly: boolean;
}

export interface RunReport {
  timestamp: string;
  searchConfig: SearchConfig;
  totalScraped: number;
  totalMatched: number;
  jobMatches: JobMatch[];
}
