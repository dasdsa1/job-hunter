import chalk from 'chalk';

export class RateLimiter {
  private timestamps: number[] = [];
  private readonly windowMs = 60_000;
  private readonly maxRequests: number;

  constructor(requestsPerMinute: number) {
    this.maxRequests = requestsPerMinute;
  }

  async throttle(): Promise<void> {
    const now = Date.now();
    this.timestamps = this.timestamps.filter((t) => now - t < this.windowMs);

    if (this.timestamps.length >= this.maxRequests) {
      const oldest = this.timestamps[0];
      const waitMs = this.windowMs - (now - oldest) + 100;
      console.log(
        chalk.gray(
          `  [rate limit] ${this.maxRequests} RPM reached — waiting ${(waitMs / 1_000).toFixed(1)}s…`,
        ),
      );
      await sleep(waitMs);
      return this.throttle();
    }

    this.timestamps.push(Date.now());
  }
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Singleton shared by matcher and coverLetter — reads GEMINI_RPM from env at startup
export const rateLimiter = new RateLimiter(
  parseInt(process.env.GEMINI_RPM ?? '15', 10),
);
