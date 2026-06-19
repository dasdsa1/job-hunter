import readline from 'readline';
import chalk from 'chalk';

export async function collectResume(): Promise<string> {
  console.log(chalk.cyan('\n━━━  RESUME INPUT  ━━━'));
  console.log(chalk.white('Paste your resume text below.'));
  console.log(chalk.gray('When finished, type  ---END---  on its own line and press Enter.\n'));

  const lines: string[] = [];

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: true,
  });

  return new Promise((resolve) => {
    rl.on('line', (line) => {
      if (line.trim() === '---END---') {
        rl.close();
      } else {
        lines.push(line);
      }
    });

    rl.on('close', () => {
      const resume = lines.join('\n').trim();
      if (resume.length < 50) {
        console.log(chalk.red('Resume seems too short. Make sure you pasted the full text.'));
      }
      resolve(resume);
    });
  });
}
