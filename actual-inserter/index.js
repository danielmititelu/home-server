import fs from 'fs';
import path from 'path';
import csv from 'csv-parser';
import chokidar from 'chokidar';
import dotenv from 'dotenv';
dotenv.config();

import {
  init,
  shutdown,
  downloadBudget,
  getCategories,
  importTransactions
} from '@actual-app/api';

const {
  ACTUAL_URL,
  ACTUAL_PASSWORD,
  ACTUAL_BUDGET_ID,
  ACTUAL_ACCOUNT_ID,
  WATCH_DIR = '/data',
} = process.env;

async function setupActual() {
  await init({
    dataDir: '/cache',
    serverURL: ACTUAL_URL,
    password: ACTUAL_PASSWORD,
  });
  await downloadBudget(ACTUAL_BUDGET_ID);
  console.log('✅ Connected and budget synced');
}

async function fetchCategoryMap() {
  const categories = await getCategories();
  const categoryMap = new Map();
  for (const cat of categories) {
    categoryMap.set(cat.name, cat.id);
  }
  return categoryMap;
}

function writeCategoryNamesCSV(categoryMap) {
  const outFile = path.join(WATCH_DIR, 'categories.csv');
  const lines = ['name'];
  for (const name of categoryMap.keys()) {
    lines.push(`"${name.replace(/"/g, '""')}"`);
  }
  fs.writeFileSync(outFile, lines.join('\n'));
  console.log(`✅ Categories written to ${outFile}`);
}

async function importCSVTransactions(file, categoryMap) {
  console.log(`📥 Found new file: ${file}`);
  const transactions = [];
  return new Promise((resolve, reject) => {
    fs.createReadStream(file)
      .pipe(csv())
      .on('data', (row) => {
        const categoryId = categoryMap.get(row.category);
        if (!categoryId) {
          reject(new Error(`Category not found for name: ${row.category}`));
        }
        transactions.push({
          date: row.date,
          amount: parseInt(row.amount, 10) * -100,
          category: categoryId,
          notes: row.notes || '',
        });
      })
      .on('end', async () => {
        console.log(`🧾 Importing ${transactions.length} transactions...`);
        try {
          const accountId = ACTUAL_ACCOUNT_ID;
          await importTransactions(accountId, transactions);
          console.log(`✅ Imported ${transactions.length} transactions from ${path.basename(file)}`);
          fs.unlinkSync(file);
          resolve();
        } catch (err) {
          console.error('❌ Failed to import transactions:', err);
          reject(err);
        }
      });
  });
}

async function main() {
  fs.mkdirSync(WATCH_DIR, { recursive: true });
  await setupActual();
  let categoryMap = await fetchCategoryMap();
  writeCategoryNamesCSV(categoryMap);

  fs.watch(WATCH_DIR, (eventType, filename) => {
    if (
      filename &&
      filename.startsWith('transaction-') &&
      filename.endsWith('.csv') &&
      eventType === 'rename'
    ) {
      const filePath = path.join(WATCH_DIR, filename);
      // Check if file exists (rename can be delete or create)
      if (fs.existsSync(filePath)) {
        console.log(`[fs.watch] Importing new file: ${filename}`);
        importCSVTransactions(filePath, categoryMap)
          .catch(err => console.error('Error importing file:', err));
      }
    }
  });


  process.on('SIGINT', async () => {
    console.log('\n🧹 Shutting down...');
    await shutdown();
    process.exit(0);
  });
}

main().catch(console.error);
