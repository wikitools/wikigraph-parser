import gzip
import json
import time
import sys
import os

# Title:    Page and category json list creator
# Author:   Mikolaj Mirko
# Usage:    titles-creator.py <categories/pages> <DUMP_FILE_PATH> [OUTPUT_FILE_NAME]
# Examples: titles-creator.py categories enwiki-20190820-category.sql.gz
#           titles-creator.py pages enwiki-20190820-page.sql pages-data.json

# Note:
# Accepted dump types: category and page (sql and sql gzipped)
# Python3.7 64bit version needed for large page sql file!
# It takes about 10min with 16GB of RAM to create json file for pages and 0.5min for categories
# Good luck :)

TYPE = sys.argv[1] if len(sys.argv) >= 2 else None
DUMP_PATH = sys.argv[2] if len(sys.argv) >= 3 else None
OUTFILE = sys.argv[3] if len(sys.argv) >= 4 else TYPE + '.json'

def readDump():
    print('\tReading dump file...')
    json_object = {}
    json_object[TYPE] = []
    fileToOpen = DUMP_PATH[0:-3] if DUMP_PATH.endswith("gz") else DUMP_PATH
    with open(fileToOpen, encoding="UTF-8", errors='ignore') as f:
        for line in f:
            if not line.startswith('INSERT INTO') or len(line.split(' ')) != 5:
                continue
            inserts = line.split(' ')[4:]
            inserts = ''.join(inserts)
            values = inserts.split('),(')
            for value in values:
                data = value.split(',')
                json_object[TYPE].append({
                    data[0]: data[1][1:-1] if TYPE == 'categories' else data[2][1:-1]
                })
    print('\tReading dump file complete.')
    print('\tCreating json file...')
    with open(OUTFILE, 'w') as outfile:
        json.dump(json_object, outfile)
    print('\tJson file created.')
    return

def start_process():
    if TYPE is None:
        print("No creator type given.")
        print("Usage: titles-creator.py <categories/pages> <DUMP_FILE_PATH> [OUTPUT_FILE_NAME]")
        return
    if DUMP_PATH is None:
        print("No dump file path given.")
        print("Usage: titles-creator.py <categories/pages> <DUMP_FILE_PATH> [OUTPUT_FILE_NAME]")
        return
    if not os.path.isfile(DUMP_PATH):
        print("Given dump file path does not exist.")
        return
    if not (DUMP_PATH.endswith("gz") or DUMP_PATH.endswith("sql")):
        print("Given dump file have format different than sql or gz.")
        return
    print('List of ' + TYPE + ' creation started...')
    start = time.time()
    if DUMP_PATH.endswith("gz"):
        print('\tDecompressing gz file...')
        sqlFile = open(DUMP_PATH[0:-3], "wb")
        with gzip.open(DUMP_PATH, "rb") as f:
            binddata = f.read()
        sqlFile.write(binddata)
        sqlFile.close()
        print('\tDecompressing gz file done.')
    readDump()
    if DUMP_PATH.endswith("gz") and os.path.exists(DUMP_PATH[0:-3]):
        os.remove(DUMP_PATH[0:-3])
    print('List of ' + TYPE + ' created in: ' + str(time.time() - start) + 's.')

start_process()
