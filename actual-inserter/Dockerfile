FROM node:20-slim 
WORKDIR /usr/src/app
COPY package*.json ./
RUN npm install
RUN mkdir -p /cache
COPY . .
CMD ["node", "index.js"]