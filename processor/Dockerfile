FROM python:3.13-slim
WORKDIR /processor
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt && useradd --uid 10001 --create-home processor
COPY server.py inspect_file.py ./
USER processor
EXPOSE 8090
CMD ["python", "server.py"]
