document.addEventListener('DOMContentLoaded', async () => {
    const urlInput = document.getElementById('urlInput');
    const shortenBtn = document.getElementById('shortenBtn');
    const resultContainer = document.getElementById('result');
    const shortUrlInput = document.getElementById('shortUrl');
    const copyBtn = document.getElementById('copyBtn');
    const shortUrlLink = document.getElementById('shortUrlLink');
    const linkList = document.getElementById('linkList');

    const API_BASE_URL = 'http://localhost:5258';

    async function fetchUrls() {
        try {
            const response = await fetch(`${API_BASE_URL}/urls`);
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }

            const urls = await response.json();

            // Ensure that urls is an array before using forEach
            if (!Array.isArray(urls)) {
                console.error("Invalid API response format:", urls);
                return;
            }

            linkList.innerHTML = '';

            urls.forEach(({ Url, ShortUrl }) => { // Ensure property names match API response
                if (!Url || !ShortUrl) return;

                const shortPath = ShortUrl.split('/').pop();
                const row = document.createElement('tr');
                row.innerHTML = `
                <td><a href="${Url}" target="_blank">${Url}</a></td>
                <td><a href="${ShortUrl}" target="_blank">${ShortUrl}</a></td>
                <td><button class="delete-btn" data-short="${shortPath}">Delete</button></td>
            `;
                linkList.appendChild(row);
            });
        } catch (error) {
            console.error('Error fetching URLs:', error);
        }
    }


    async function shortenUrl() {
        const url = urlInput.value.trim();
        if (!url) return alert('Please enter a URL');
        try {
            const response = await fetch(`${API_BASE_URL}/shorturl`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ url }),
            });
            if (!response.ok) throw new Error(await response.text());
            const data = await response.json();
            shortUrlInput.value = data.shortUrl;
            shortUrlLink.href = data.shortUrl;
            shortUrlInput.value = data.url;
            shortUrlLink.href = data.url;
            resultContainer.classList.remove('hidden');
            fetchUrls()
        } catch (error) {
            alert('Error: ' + error.message);
        }
    }

    async function deleteUrl(shortUrl) {
        try {
            const response = await fetch(`${API_BASE_URL}/delete/${shortUrl}`, { method: 'DELETE' });
            if (!response.ok) throw new Error(await response.text());
            fetchUrls();
        } catch (error) {
            alert('Error deleting URL: ' + error.message);
        }
    }

    shortenBtn.addEventListener('click', shortenUrl);
    urlInput.addEventListener('keypress', (e) => { if (e.key === 'Enter') shortenUrl(); });
    copyBtn.addEventListener('click', () => {
        navigator.clipboard.writeText(shortUrlInput.value);
        copyBtn.textContent = 'Copied!';
        setTimeout(() => copyBtn.textContent = 'Copy', 2000);
    });
    linkList.addEventListener('click', (e) => {
        if (e.target.classList.contains('delete-btn')) {
            const shortUrl = e.target.dataset.short;
            deleteUrl(shortUrl);
        }
    });

    fetchUrls();
});