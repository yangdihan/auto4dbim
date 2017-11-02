import os.path
import sys
import random
from operator import itemgetter
from collections import defaultdict
from math import log, exp, inf
import time
import pprint
import json

def readFileToCorpus(f):
	""" Reads in the text file f which contains one sentence per line.
	"""
	if os.path.isfile(f):
		file = open(f, "r") # open the input file in read-only mode
		i = 0 # this is just a counter to keep track of the sentence numbers
		corpus = [] # this will become a list of sentences
		print("Reading file ", f)
		for line in file:
			i += 1
			corpus.append(line)
        #endfor
		pprint.pprint(corpus)
		return corpus
	else:
	#ideally we would throw an exception here, but this will suffice
		print("Error: corpus file ", f, " does not exist")
		sys.exit() # exit the script
	#endif
#enddef

def demo(corpus, levelDict, zoneDict):
    pp = pprint.PrettyPrinter(indent=4)
    ret = defaultdict()
    ret['Levels'] = defaultdict()
    for i in levelDict['L1']:
        ret['Levels'][i] = defaultdict()
        ret['Levels'][i]['Zones'] = defaultdict()
    #look for terms in corpus
    for words in corpus:
        #search through l1word array
        for l1k in levelDict['L1']:
            if l1k in words:
                for l2k in levelDict['L2']:
                    if l2k in words:
                        if l2k not in ret['Levels'][l1k]['Zones']:
                            ret['Levels'][l1k]['Zones'][l2k] = defaultdict()
                        ret['Levels'][l1k]['Zones'][l2k] = zoneDict[l2k]
#                         if l1keyword not in ret:
#                             ret[l1keyword] = defaultdict()
#                         else:
#                             if l2keyword not in ret[l1keyword]:
#                                 ret[l1keyword][l2keyword] = defaultdict()
#                                 ret[l1keyword][l2keyword] = zoneDict[l2keyword]
#                             else:
#                                 ret[l1keyword][l2keyword] = zoneDict[l2keyword]
    pp.pprint(ret)
    jsonversion = json.dumps(ret)
    #pp.pprint(jsonversion)
    return ret

if __name__ == "__main__":
	#initialize dictionaries for use in demo, comment out if needed 
	# hope to replace with user input in future
	areaDictionary = defaultdict()
	areaDictionary['zone 1'] = {'top': 'A', 'bottom': 'B', 'left' : '1', 'right' : '2'}
	areaDictionary['zone 2'] = {'top': 'A', 'bottom': 'B', 'left' : '2', 'right' : '4'}
	areaDictionary['zone 3'] = {'top': 'B', 'bottom': 'C', 'left' : '1', 'right' : '4'}

	areaDictionary['zone 4'] = {'top': 'A', 'bottom': 'C', 'left' : '1', 'right' : '3'}
	areaDictionary['zone 5'] = {'top': 'A', 'bottom': 'B', 'left' : '3', 'right' : '4'}
	areaDictionary['zone 6'] = {'top': 'B', 'bottom': 'C', 'left' : '3', 'right' : '4'}

	levelsDict =  defaultdict()
	levelsDict['L1'] = ['Level 1', 'Level 2']
	levelsDict['L2'] = ['zone 1', 'zone 2', 'zone 3', 'zone 4', 'zone 5', 'zone 6']
	#read your corpora
	trainCorpus = readFileToCorpus('Schedule_Hotel_demo.csv')
	#pprint.pprint(trainCorpus)
	#trainCorpus = preprocess(trainCorpus)
	stats = demo(trainCorpus, levelsDict, areaDictionary)
	# with open('parsed.json', 'w') as outfile:
	# 	json.dump(stats, outfile)
	outfile = open('parsed.json', 'w')
	outfile.write(json.dumps(stats, indent=4, sort_keys=False))
	outfile.close()
        
